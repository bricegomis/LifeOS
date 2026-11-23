"""Unit tests for garmin-sync: mapping and the connection loop, with fakes for Garmin and LifeOS.

Run from src/garmin-sync: python -m unittest discover -s tests
"""

import sys
import tempfile
import unittest
from datetime import date, datetime, timedelta, timezone
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import garmin_sync  # noqa: E402


ACTIVITY = {
    "activityId": 101, "activityName": "Course", "activityType": {"typeKey": "running"},
    "startTimeGMT": "2026-11-09 11:14:53", "duration": 1680.4, "elapsedDuration": 1720.0, "distance": 4680.0,
    "elevationGain": 41.0, "calories": 331.0, "averageHR": 133.0, "maxHR": 150.0, "averageSpeed": 2.78,
    "aerobicTrainingEffect": 2.700000047683716, "anaerobicTrainingEffect": 0.8, "vO2MaxValue": 49.0,
    "hrTimeInZone_1": 180.0, "hrTimeInZone_2": 540.0, "hrTimeInZone_3": 840.0, "hrTimeInZone_4": 120.0,
}


class MappingTests(unittest.TestCase):
    def test_activity_keeps_garmin_units_and_raw_answer(self):
        body = garmin_sync.activity_request(ACTIVITY)
        self.assertEqual(body["type"], "running")
        self.assertEqual(body["startedAt"], "2026-11-09T11:14:53+00:00")
        self.assertEqual(body["heartRateZoneSeconds"], [180.0, 540.0, 840.0, 120.0, 0])
        self.assertIsNone(body["directWorkoutRpe"])
        self.assertIs(body["raw"], ACTIVITY)

    def test_out_of_range_values_are_dropped_not_sent(self):
        body = garmin_sync.activity_request(dict(ACTIVITY, maxHR=900, distance=-1))
        self.assertIsNone(body["maxHeartRate"])
        self.assertIsNone(body["distanceMeters"])
        self.assertIsNone(garmin_sync.activity_request(dict(ACTIVITY, startTimeGMT=None)))

    def test_vo2max_reads_the_running_and_cycling_blocks(self):
        data = [{"generic": {"calendarDate": "2026-11-09", "vo2MaxPreciseValue": 49.2, "vo2MaxValue": 49.0}, "cycling": None}]
        self.assertEqual(garmin_sync.vo2_measures(data, "running"), [{"date": "2026-11-09", "sport": "running", "value": 49.2}])
        self.assertEqual(garmin_sync.vo2_measures({"maxMetrics": []}, "cycling"), [])

    def test_a_day_joins_sleep_hrv_and_stats(self):
        sleep = {"calendarDate": "2026-11-23", "values": {"remTime": 2340, "restingHeartRate": 49, "totalSleepTimeInSeconds": 22380,
                 "respiration": 12.5, "deepTime": 5220, "awakeTime": 3360, "lightTime": 14820, "sleepScoreQuality": "FAIR",
                 "localSleepStartTimeInMillis": 1795387200000, "localSleepEndTimeInMillis": 1795410381000}}
        hrv = {"hrvSummary": {"weeklyAvg": 42, "lastNightAvg": 43, "status": "BALANCED", "baseline": {"balancedLow": 37, "balancedUpper": 47}}}
        stats = {"totalSteps": 4210, "averageStressLevel": 30, "bodyBatteryAtWakeTime": 62}
        day = garmin_sync.wellness_day(date(2026, 11, 23), sleep, hrv, stats)
        self.assertEqual(day["sleepSeconds"], 22380)
        self.assertEqual(day["hrvBaselineLow"], 37)
        self.assertEqual(day["steps"], 4210)
        self.assertEqual(day["bodyBatteryWake"], 62)
        self.assertTrue(day["sleepEnd"].endswith("+01:00"))
        self.assertEqual(set(day["raw"]), {"sleep", "hrv", "stats"})


class FakeLifeOS:
    def __init__(self):
        self.reports = []
        self.queue = []

    def commands(self):
        return self.queue.pop(0) if self.queue else {}

    def report(self, state, message=None, summary=None, next_sync=None):
        self.reports.append(state)


class FakeGarmin:
    """Saved tokens are refused; the password login needs a code; 123456 is the right one."""
    instances = []

    def __init__(self, email=None, password=None, return_on_mfa=False):
        self.email = email
        self.client = mock.Mock()
        FakeGarmin.instances.append(self)

    def login(self, tokenstore=None):
        if self.email is None:
            raise RuntimeError("no tokens")
        return "needs_mfa", {"state": 1}

    def resume_login(self, state, code):
        if code != "123456":
            raise RuntimeError("bad code")
        return None, None


class LoopTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        env = {"GARMIN_EMAIL": "a@b.c", "GARMIN_PASSWORD": "x", "LIFEOS_IMPORT_TOKEN": "lifeos_imp_x", "GARMIN_SYNC_DATA": self.tmp.name}
        self.patches = [mock.patch.dict("os.environ", env), mock.patch.object(garmin_sync, "Garmin", FakeGarmin),
                        mock.patch.object(garmin_sync, "sync", return_value="1 activité envoyée")]
        for p in self.patches:
            p.start()
        self.service = garmin_sync.Service()
        self.service.lifeos = FakeLifeOS()

    def tearDown(self):
        for p in self.patches:
            p.stop()
        self.tmp.cleanup()

    def test_a_wrong_code_is_refused_then_the_right_one_connects_and_syncs(self):
        self.assertEqual(self.service.step(), garmin_sync.MFA_POLL_SECONDS)
        self.assertEqual(self.service.lifeos.reports, ["mfa_requested"])
        self.service.lifeos.queue = [{"mfaCode": "000000"}]
        self.service.step()
        self.assertEqual(self.service.lifeos.reports[-1], "code_refused")
        self.assertIsNotNone(self.service.pending)
        self.service.lifeos.queue = [{"mfaCode": "123456"}]
        self.service.step()
        self.assertEqual(self.service.lifeos.reports[-1], "ok")
        self.assertIsNone(self.service.pending)

    def test_an_unanswered_code_expires_and_is_not_asked_again_before_the_next_planned_sync(self):
        self.service.step()
        garmin, state, _ = self.service.pending
        self.service.pending = (garmin, state, datetime.now(timezone.utc) - timedelta(minutes=16))
        self.service.step()
        self.assertEqual(self.service.lifeos.reports[-1], "code_expired")
        self.assertGreater(self.service.next_run, datetime.now(garmin_sync.PARIS))
        self.service.step()
        self.assertEqual(self.service.lifeos.reports[-1], "code_expired")


if __name__ == "__main__":
    unittest.main()
