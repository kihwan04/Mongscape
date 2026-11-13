FROM python:3.12-slim-bookworm

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    IDENTITY_HOST=0.0.0.0
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends libglib2.0-0 \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd --gid 10001 identity \
    && useradd --uid 10001 --gid identity --no-create-home identity
COPY requirements.txt ./
RUN pip install --no-cache-dir --only-binary=:all: -r requirements.txt
COPY setup_models.py ./
COPY models/provenance.json models/
RUN python setup_models.py
COPY server.py storage.py schema.postgres.sql migrate_account.py ./
USER 10001:10001
EXPOSE 8765
CMD ["python", "server.py"]
