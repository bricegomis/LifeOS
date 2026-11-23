# garmin-sync

Copies Garmin Connect into LifeOS ([ADR 0019](../../docs/architecture/decisions/0019-garmin-import.md)):
activities become sport sessions, VO2max is kept per day and sport, sleep and recovery go to the
Health module. It reads Garmin with the unofficial
[python-garminconnect](https://github.com/cyberjunky/python-garminconnect) at a pinned version and
writes through the LifeOS import endpoints with an import token. LifeOS itself never calls Garmin.

## Configuration (variables of the LifeOS stack)

| Variable | Role |
| --- | --- |
| `GARMIN_EMAIL`, `GARMIN_PASSWORD` | Garmin Connect account |
| `LIFEOS_IMPORT_TOKEN` | Token created in LifeOS, Sport › Garmin › Jeton d'import |
| `GARMIN_SYNC_API_URL` | Optional; the API seen from the container, `http://api:8080` by default |
| `GARMIN_SYNC_HOURS` | Optional; Paris hours of the planned syncs, `7,13,19,23` by default |

Without the first three, the container stays idle. Garmin tokens and the sync progress live in
`/data` (volume `lifeos_garmin_tokens`, to back up with the others).

## Double authentication

When Garmin asks for a code, the pending login stays in memory, LifeOS shows "Garmin attend un
code" and the code is typed in LifeOS (Sport › Garmin). The service polls for it every 15 seconds,
gives up after 15 minutes ("code expiré") and asks again only at the next planned sync or when
"Relancer la connexion Garmin" is pressed. No terminal on the server is needed.

## Updating the library

The version is pinned in `requirements.txt` because the library follows Garmin's private login and
breaks when Garmin changes it. Upgrade by hand after checking its issues; nothing is lost meanwhile,
the next sync catches up.

## Tests

```bash
pip install -r requirements.txt
python -m unittest discover -s tests
```
