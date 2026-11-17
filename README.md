# TaxMate Report Parser API

Бэкенд для парсинга брокерских отчётов Ultima/BCS, расчёта налоговой базы и выгрузки Excel-пояснения к 3‑НДФЛ.

## Структура

- `Api` — HTTP (FastEndpoints)
- `Application` — оркестрация и налоговый калькулятор
- `Domain` — модели отчёта и налоговой базы
- `Infrastructure` — PDF-парсер, курсы ЦБ, Excel, session store
- `Shared` — общие контракты

## Запуск

```bash
cd Api
dotnet run
```

API: `http://localhost:5080`  
Swagger: `http://localhost:5080/swagger`

## Endpoints

- `POST /api/reports/ultima-bcs/parse` — загрузка PDF
- `POST /api/reports/ultima-bcs/recalculate` — пересчёт с ручными расходами
- `POST /api/reports/ultima-bcs/export` — Excel-пояснение
- `GET /api/health`
