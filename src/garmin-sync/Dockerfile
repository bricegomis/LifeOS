FROM python:3.13-slim

WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt \
    && useradd --create-home --uid 10001 garminsync \
    && mkdir -p /data && chown garminsync:garminsync /data
COPY garmin_sync.py .

USER garminsync
ENV GARMIN_SYNC_DATA=/data PYTHONUNBUFFERED=1
VOLUME ["/data"]
CMD ["python", "garmin_sync.py"]
