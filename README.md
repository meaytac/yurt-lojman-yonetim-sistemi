# MTU Universite Lojman ve Yurt Yonetim Sistemi

ASP.NET Core Web API, Entity Framework Core Code-First, ASP.NET Core Identity, JWT, Swagger, vanilla HTML/CSS/JS frontend ve WhatsApp Cloud API webhook uyumlu bot endpointleri iceren temel projedir.

## Calistirma

1. SQL Server veya SQL Server Express/LocalDB kurulu olmalidir.
2. `appsettings.json` icindeki `ConnectionStrings:DefaultConnection` degerini ortamınıza gore guncelleyin.
3. Asagidaki komutlari calistirin:

```bash
dotnet restore
dotnet ef database update
dotnet run
```

Uygulama acildiginda `/swagger`, `/index.html`, `/admin.html` ve `/application.html` adresleri kullanilabilir.

## Varsayilan Admin

- E-posta: `admin@ozal.edu.tr`
- Sifre: `Admin123!`

## Ana API Gruplari

- `POST /api/auth/register`, `POST /api/auth/login`
- `GET/POST/PUT/DELETE /api/admin/dormitories`
- `GET/POST/PUT/DELETE /api/admin/housing-units`
- `GET/POST/PUT/DELETE /api/admin/buildings`
- `GET/POST/DELETE /api/admin/floors`
- `GET/POST/PUT/DELETE /api/admin/rooms`
- `GET /api/admin/dashboard`
- `POST /api/applications`, `POST /api/applications/{id}/decision`
- `GET/POST /api/payments`, `POST /api/payments/{id}/paid`
- `GET/POST /api/requests`, `PATCH /api/requests/{id}/status`
- `GET/POST /api/announcements`
- `GET/POST /api/bot/webhook`
- `GET /api/bot/check-application?tcNo=...`
- `GET /api/bot/check-debt?tcNo=...`
- `POST /api/bot/create-request`
- `GET /api/bot/announcements`
