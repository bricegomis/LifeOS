"""garmin-sync: copies Garmin Connect into LifeOS (ADR 0019).

Reads Garmin Connect with the unofficial python-garminconnect library (pinned version) and writes
into LifeOS through its import endpoints, with an import token. LifeOS never calls Garmin.

Configuration (environment):
    GARMIN_EMAIL, GARMIN_PASSWORD   Garmin Connect account
    LIFEOS_API_URL                  LifeOS API as seen from the container (default http://api:8080)
    LIFEOS_IMPORT_TOKEN             import token created in LifeOS settings
    GARMIN_SYNC_HOURS               Paris hours of the planned syncs (default "7,13,19,23")
    GARMIN_SYNC_DATA                folder for Garmin tokens and progress (default /data)

Double authentication: when Garmin asks for a code, the pending login stays in memory only, the
service reports "mfa_requested" and polls LifeOS every 15 seconds for the code typed in the app;
after 15 minutes it gives up ("code_expired") and waits for the next planned sync or a new
connection asked in the app.
"""

from __future__ import annotations

import hashlib
import json
import logging
import os
import shutil
import sys
import time
from dataclasses import dataclass, field
from datetime import date, datetime, timedelta, timezone
from pathlib import Path
from typing import Any
from zoneinfo import ZoneInfo

import requests
from garminconnect import Garmin

PARIS = ZoneInfo("Europe/Paris")
CODE_LIFETIME = timedelta(minutes=15)
POLL_SECONDS = 300
MFA_POLL_SECONDS = 15
DAILY_WINDOW = 7          # days re-read at every sync (Garmin completes them afterwards)
BACKFILL_STEP = 60        # older days of sleep and recovery fetched per sync until the history is complete
VO2_WINDOW = 30

log = logging.getLogger("garmin-sync")


# ── LifeOS ─────────────────────────────────────────────────────────────────────────────────────

class LifeOS:
    def __init__(self, url: str, token: str) -> None:
        self.url = url.rstrip("/")
        self.session = requests.Session()
        self.session.headers["X-LifeOS-Import-Token"] = token

    def _call(self, method: str, path: str, body: Any = None) -> Any:
        response = self.session.request(method, self.url + path, json=body, timeout=60)
        if response.status_code >= 400:
            raise LifeOSError(f"{method} {path}: {response.status_code} {response.text[:300]}", response.status_code)
        return response.json() if response.content else None

    def commands(self) -> dict[str, Any]:
        return self._call("POST", "/api/import/garmin/commands") or {}

    def report(self, state: str, message: str | None = None, summary: str | None = None,
               next_sync: datetime | None = None) -> None:
        try:
            self._call("POST", "/api/import/garmin/status", {
                "state": state, "message": message, "summary": summary,
                "nextSyncAt": next_sync.astimezone(timezone.utc).isoformat() if next_sync else None,
            })
        except Exception as e:  # a lost report must not stop the service
            log.warning("status report failed: %s", e)

    def put_activity(self, activity_id: int, body: dict[str, Any]) -> None:
        self._call("PUT", f"/api/import/garmin/activities/{activity_id}", body)

    def reconcile(self, ids: list[int]) -> int:
        return (self._call("POST", "/api/import/garmin/activities/reconcile", {"activityIds": ids}) or {}).get("deleted", 0)

    def vo2max(self, measures: list[dict[str, Any]]) -> None:
        for i in range(0, len(measures), 500):
            self._call("POST", "/api/import/garmin/vo2max", {"measures": measures[i:i + 500]})

    def wellness(self, days: list[dict[str, Any]]) -> None:
        for i in range(0, len(days), 50):
            self._call("POST", "/api/import/garmin/wellness", {"days": days[i:i + 50]})


class LifeOSError(Exception):
    def __init__(self, message: str, status: int) -> None:
        super().__init__(message)
        self.status = status


# ── Mapping Garmin → LifeOS import requests ──────────────────────────────────────────────────

def _num(value: Any, low: float | None = None, high: float | None = None) -> float | None:
    if not isinstance(value, (int, float)) or isinstance(value, bool):
        return None
    if (low is not None and value < low) or (high is not None and value > high):
        return None
    return float(value)


def _int(value: Any, low: int | None = None, high: int | None = None) -> int | None:
    number = _num(value, low, high)
    return int(round(number)) if number is not None else None


def gmt(text: str | None) -> datetime | None:
    """Garmin's "2026-11-22 15:24:48" GMT strings."""
    if not text:
        return None
    return datetime.strptime(text[:19], "%Y-%m-%d %H:%M:%S").replace(tzinfo=timezone.utc)


def activity_request(a: dict[str, Any]) -> dict[str, Any] | None:
    start = gmt(a.get("startTimeGMT"))
    duration = _num(a.get("duration"), 1, 86400) or _num(a.get("elapsedDuration"), 1, 86400)
    if start is None or duration is None:
        return None
    zones = [_num(a.get(f"hrTimeInZone_{i}"), 0, 86400) for i in range(1, 6)]
    return {
        "type": ((a.get("activityType") or {}).get("typeKey") or "other")[:60],
        "name": (a.get("activityName") or None),
        "startedAt": start.isoformat(),
        "durationSeconds": duration,
        "elapsedSeconds": _num(a.get("elapsedDuration"), 1, 86400),
        "distanceMeters": _num(a.get("distance"), 0, 1_000_000),
        "elevationGainMeters": _num(a.get("elevationGain"), 0, 20000),
        "calories": _num(a.get("calories"), 0, 20000),
        "averageHeartRate": _num(a.get("averageHR"), 30, 250),
        "maxHeartRate": _num(a.get("maxHR"), 30, 250),
        "averageSpeedMetersPerSecond": _num(a.get("averageSpeed"), 0, 100),
        "aerobicTrainingEffect": _num(a.get("aerobicTrainingEffect"), 0, 5),
        "anaerobicTrainingEffect": _num(a.get("anaerobicTrainingEffect"), 0, 5),
        "directWorkoutRpe": _int(a.get("directWorkoutRpe"), 0, 100),
        "directWorkoutFeel": _int(a.get("directWorkoutFeel"), 0, 100),
        "heartRateZoneSeconds": [z or 0 for z in zones] if any(z for z in zones) else None,
        "raw": a,
    }


def vo2_measures(data: Any, sport: str) -> list[dict[str, Any]]:
    entries = data if isinstance(data, list) else ((data or {}).get("maxMetrics") or [])
    out = []
    for entry in entries:
        if not isinstance(entry, dict):
            continue
        for key in ("generic", "cycling"):
            block = entry.get(key) or {}
            value = _num(block.get("vo2MaxPreciseValue"), 10, 100) or _num(block.get("vo2MaxValue"), 10, 100)
            day = block.get("calendarDate") or entry.get("calendarDate")
            if value and day:
                out.append({"date": day[:10], "sport": "cycling" if key == "cycling" else sport, "value": round(value, 1)})
    return out


def _local_millis(ms: Any) -> datetime | None:
    """Garmin's "local" epoch milliseconds: the Paris wall clock written as if it were UTC."""
    if not isinstance(ms, (int, float)) or ms <= 0:
        return None
    naive = datetime.fromtimestamp(ms / 1000, tz=timezone.utc).replace(tzinfo=None)
    return naive.replace(tzinfo=PARIS)


def wellness_day(day: date, sleep: dict[str, Any] | None, hrv: dict[str, Any] | None, stats: dict[str, Any] | None) -> dict[str, Any]:
    v = (sleep or {}).get("values") or {}
    summary = (hrv or {}).get("hrvSummary") or {}
    baseline = summary.get("baseline") or {}
    s = stats or {}
    start = _local_millis(v.get("localSleepStartTimeInMillis"))
    end = _local_millis(v.get("localSleepEndTimeInMillis"))
    if start and end and end <= start:
        start = end = None
    score = v.get("sleepScore") if isinstance(v.get("sleepScore"), (int, float)) else (v.get("overallScore") or None)
    return {
        "date": day.isoformat(),
        "sleepStart": start.isoformat() if start else None,
        "sleepEnd": end.isoformat() if end else None,
        "sleepSeconds": _int(v.get("totalSleepTimeInSeconds"), 0, 86400),
        "deepSleepSeconds": _int(v.get("deepTime"), 0, 86400),
        "lightSleepSeconds": _int(v.get("lightTime"), 0, 86400),
        "remSleepSeconds": _int(v.get("remTime"), 0, 86400),
        "awakeSeconds": _int(v.get("awakeTime"), 0, 86400),
        "sleepScore": _int(score, 0, 100),
        "sleepQuality": (v.get("sleepScoreQuality") or None),
        "restingHeartRate": _int(v.get("restingHeartRate"), 20, 200) or _int(s.get("restingHeartRate"), 20, 200),
        "hrvNightAverage": _int(summary.get("lastNightAvg"), 1, 400),
        "hrvWeeklyAverage": _int(summary.get("weeklyAvg"), 1, 400),
        "hrvBaselineLow": _int(baseline.get("balancedLow"), 1, 400),
        "hrvBaselineHigh": _int(baseline.get("balancedUpper"), 1, 400),
        "hrvStatus": (summary.get("status") or None),
        "respiration": _num(v.get("respiration"), 0, 80),
        "spO2": _num(s.get("averageSpo2"), 0, 100),
        "bodyBatteryWake": _int(s.get("bodyBatteryAtWakeTime"), 0, 100),
        "bodyBatteryHigh": _int(s.get("bodyBatteryHighestValue"), 0, 100),
        "bodyBatteryLow": _int(s.get("bodyBatteryLowestValue"), 0, 100),
        "stressAverage": _int(s.get("averageStressLevel"), 0, 100),
        "steps": _int(s.get("totalSteps"), 0, 500_000),
        "raw": {"sleep": sleep, "hrv": hrv, "stats": stats},
    }


# ── State kept between runs ──────────────────────────────────────────────────────────────────

@dataclass
class Progress:
    path: Path
    activity_hashes: dict[str, str] = field(default_factory=dict)
    history_start: str | None = None
    wellness_back_to: str | None = None
    vo2_done: bool = False

    @classmethod
    def load(cls, path: Path) -> "Progress":
        try:
            data = json.loads(path.read_text())
            return cls(path, data.get("activity_hashes", {}), data.get("history_start"), data.get("wellness_back_to"), data.get("vo2_done", False))
        except (OSError, ValueError):
            return cls(path)

    def save(self) -> None:
        tmp = self.path.with_suffix(".tmp")
        tmp.write_text(json.dumps({"activity_hashes": self.activity_hashes, "history_start": self.history_start,
                                   "wellness_back_to": self.wellness_back_to, "vo2_done": self.vo2_done}))
        tmp.replace(self.path)


# ── Sync ─────────────────────────────────────────────────────────────────────────────────────

def chunks(start: date, end: date, days: int):
    current = start
    while current <= end:
        stop = min(end, current + timedelta(days=days - 1))
        yield current, stop
        current = stop + timedelta(days=1)


def sync(garmin: Garmin, lifeos: LifeOS, progress: Progress) -> str:
    today = datetime.now(PARIS).date()

    # Activities: the whole list each time (a few pages), so deletions in Garmin Connect follow.
    activities: list[dict[str, Any]] = []
    while True:
        page = garmin.get_activities(len(activities), 100) or []
        activities.extend(a for a in page if isinstance(a, dict))
        if len(page) < 100:
            break
    sent = 0
    for a in activities:
        body = activity_request(a)
        activity_id = a.get("activityId")
        if body is None or not isinstance(activity_id, int):
            continue
        digest = hashlib.sha256(json.dumps(a, sort_keys=True, default=str).encode()).hexdigest()
        if progress.activity_hashes.get(str(activity_id)) == digest:
            continue
        try:
            lifeos.put_activity(activity_id, body)
            progress.activity_hashes[str(activity_id)] = digest
            sent += 1
        except LifeOSError as e:
            if e.status >= 500 or e.status in (401, 403):
                raise
            log.warning("activity %s refused: %s", activity_id, e)
    ids = [a["activityId"] for a in activities if isinstance(a.get("activityId"), int)]
    deleted = lifeos.reconcile(ids) if ids else 0
    progress.activity_hashes = {k: v for k, v in progress.activity_hashes.items() if int(k) in set(ids)}
    starts = [gmt(a.get("startTimeGMT")) for a in activities]
    oldest = min((s for s in starts if s), default=None)
    if oldest and (progress.history_start is None or oldest.date().isoformat() < progress.history_start):
        progress.history_start = oldest.date().isoformat()
    progress.save()

    history = date.fromisoformat(progress.history_start) if progress.history_start else today - timedelta(days=30)

    # VO2max: the whole history once, then the last month.
    vo2_from = history if not progress.vo2_done else today - timedelta(days=VO2_WINDOW)
    measures: list[dict[str, Any]] = []
    for sport in ("running", "cycling"):
        for a, b in chunks(vo2_from, today, 90):
            try:
                measures += vo2_measures(garmin.get_max_metrics_range(a.isoformat(), b.isoformat(), "daily", sport), sport)
            except Exception as e:
                log.warning("vo2max %s %s..%s: %s", sport, a, b, e)
    unique = {(m["date"], m["sport"]): m for m in measures}
    if unique:
        lifeos.vo2max(list(unique.values()))
    progress.vo2_done = True
    progress.save()

    # Sleep and recovery: the last week, plus one older month per sync until the history is covered.
    days = [today - timedelta(days=i) for i in range(DAILY_WINDOW)]
    back_to = date.fromisoformat(progress.wellness_back_to) if progress.wellness_back_to else today - timedelta(days=DAILY_WINDOW)
    if back_to > history:
        older_end = back_to - timedelta(days=1)
        older_start = max(history, back_to - timedelta(days=BACKFILL_STEP))
        days += [older_start + timedelta(days=i) for i in range((older_end - older_start).days + 1)]
        back_to = older_start
    first, last = min(days), max(days)
    sleep_rows = {r.get("calendarDate"): r for r in (garmin.get_sleep_daily(first.isoformat(), last.isoformat()) or []) if isinstance(r, dict)}
    payload = []
    for day in sorted(set(days)):
        key = day.isoformat()
        hrv = _safe(lambda: garmin.get_hrv_data(key))
        stats = _safe(lambda: garmin.get_stats(key))
        sleep = sleep_rows.get(key)
        if sleep or hrv or stats:
            payload.append(wellness_day(day, sleep, hrv, stats))
    lifeos.wellness(payload)
    progress.wellness_back_to = back_to.isoformat()
    progress.save()

    parts = [f"{sent} activité{'s' if sent > 1 else ''} envoyée{'s' if sent > 1 else ''}", f"{len(payload)} jour{'s' if len(payload) > 1 else ''} de récupération"]
    if deleted:
        parts.append(f"{deleted} supprimée{'s' if deleted > 1 else ''}")
    return ", ".join(parts)


def _safe(call):
    try:
        return call()
    except Exception as e:
        log.debug("garmin call failed: %s", e)
        return None


# ── Connection and main loop ─────────────────────────────────────────────────────────────────

class Service:
    def __init__(self) -> None:
        self.email = os.getenv("GARMIN_EMAIL", "")
        self.password = os.getenv("GARMIN_PASSWORD", "")
        token = os.getenv("LIFEOS_IMPORT_TOKEN", "")
        self.data = Path(os.getenv("GARMIN_SYNC_DATA", "/data"))
        self.data.mkdir(parents=True, exist_ok=True)
        self.tokens = str(self.data / "garmin-tokens")
        self.hours = sorted({int(h) for h in os.getenv("GARMIN_SYNC_HOURS", "7,13,19,23").split(",") if h.strip()})
        self.lifeos = LifeOS(os.getenv("LIFEOS_API_URL", "http://api:8080"), token)
        self.progress = Progress.load(self.data / "progress.json")
        self.garmin: Garmin | None = None
        self.pending: tuple[Garmin, Any, datetime] | None = None
        self.next_run = datetime.now(PARIS)  # sync at start-up
        self.configured = bool(token and self.email and self.password)

    def planned_after(self, now: datetime) -> datetime:
        for day in range(2):
            for hour in self.hours:
                at = datetime.combine(now.date() + timedelta(days=day), datetime.min.time(), PARIS).replace(hour=hour)
                if at > now:
                    return at
        return now + timedelta(hours=6)

    def connect(self) -> bool:
        """Saved tokens first, then the password; with MFA the login waits for a code."""
        try:
            garmin = Garmin()
            garmin.login(self.tokens)
            self.garmin = garmin
            return True
        except Exception:
            pass
        try:
            garmin = Garmin(self.email, self.password, return_on_mfa=True)
            status, state = garmin.login()
        except Exception as e:
            self.garmin = None
            text = str(e)
            refused = any(word in text.lower() for word in ("401", "credential", "password", "authentication"))
            self.lifeos.report("credentials_refused" if refused else "error", text[:900], next_sync=self.next_run)
            return False
        if status == "needs_mfa":
            self.pending = (garmin, state, datetime.now(timezone.utc))
            self.lifeos.report("mfa_requested", "Garmin a envoyé un code. Saisissez-le dans LifeOS.")
            return False
        garmin.client.dump(self.tokens)
        garmin = Garmin()
        garmin.login(self.tokens)
        self.garmin = garmin
        return True

    def finish_mfa(self, code: str) -> bool:
        garmin, state, _ = self.pending  # type: ignore[misc]
        try:
            garmin.resume_login(state, code)
        except Exception as e:
            self.lifeos.report("code_refused", f"Code refusé par Garmin : {str(e)[:300]}")
            return False
        garmin.client.dump(self.tokens)
        self.pending = None
        self.garmin = garmin
        return True

    def run_sync(self) -> None:
        assert self.garmin is not None
        now = datetime.now(PARIS)
        self.next_run = self.planned_after(now)
        try:
            summary = sync(self.garmin, self.lifeos, self.progress)
            self.lifeos.report("ok", None, summary, self.next_run)
            log.info("sync done: %s", summary)
        except LifeOSError as e:
            log.error("LifeOS refused the import: %s", e)
            self.lifeos.report("error", f"LifeOS a refusé l'import : {e}"[:900], next_sync=self.next_run)
        except Exception as e:
            log.exception("sync failed")
            self.garmin = None  # reconnect next time: tokens may have expired
            self.lifeos.report("error", str(e)[:900], next_sync=self.next_run)

    def step(self) -> int:
        """One turn of the loop; returns the seconds to sleep."""
        commands = self.lifeos.commands()
        now = datetime.now(PARIS)
        if commands.get("reconnect"):
            self.garmin = None
            self.pending = None
            shutil.rmtree(self.tokens, ignore_errors=True)
            self.connect() and self.run_sync()
        elif self.pending:
            code = commands.get("mfaCode")
            if code:
                self.finish_mfa(code) and self.run_sync()
            elif datetime.now(timezone.utc) - self.pending[2] > CODE_LIFETIME:
                self.pending = None
                self.next_run = self.planned_after(now)
                self.lifeos.report("code_expired", "Aucun code saisi en 15 minutes.", next_sync=self.next_run)
        elif commands.get("sync") or now >= self.next_run:
            if self.garmin is None:
                self.next_run = self.planned_after(now)
                if not self.connect():
                    return MFA_POLL_SECONDS if self.pending else POLL_SECONDS
            self.run_sync()
        if self.pending:
            return MFA_POLL_SECONDS
        return max(5, min(POLL_SECONDS, int((self.next_run - datetime.now(PARIS)).total_seconds())))

    def run(self) -> None:
        if not self.configured:
            log.error("GARMIN_EMAIL, GARMIN_PASSWORD and LIFEOS_IMPORT_TOKEN are required; idle.")
            while True:
                time.sleep(3600)
        log.info("garmin-sync started; planned syncs at %s h (Paris)", ", ".join(map(str, self.hours)))
        while True:
            try:
                wait = self.step()
            except Exception as e:  # LifeOS unreachable: try again later
                log.warning("loop error: %s", e)
                wait = POLL_SECONDS
            time.sleep(wait)


def main() -> None:
    logging.basicConfig(level=os.getenv("LOG_LEVEL", "INFO"), format="%(asctime)s %(levelname)s %(message)s", stream=sys.stdout)
    Service().run()


if __name__ == "__main__":
    main()
