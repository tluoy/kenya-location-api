FROM python:3.13-slim
RUN apt-get update && apt-get install -y --no-install-recommends libgeos-dev libproj-dev && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY ingest.py .
COPY ingest_places.py .
COPY run_ingestion.py .
COPY download_sources.py .
ENTRYPOINT ["python", "run_ingestion.py"]
