# AI Interview Service

Standalone Python microservice for AI-powered candidate interviews.

## Setup

```bash
cd src/Services/Jobsite.AIInterview.Service
python -m venv .venv
source .venv/bin/activate
pip install -e ".[dev]"
```

## Run

```bash
uvicorn app.main:app --reload --port 5100
```

## Test

```bash
pytest
```
