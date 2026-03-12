# 🗳️ RealTimePoll API

ASP.NET Core 8 + Onion Architecture + SignalR + SQL Server

---

## 🏗️ Proje Yapısı (Onion Architecture)

```
RealTimePoll/
├── src/
│   ├── Core/
│   │   ├── RealTimePoll.Domain/          ← Entities, Interfaces, Enums (dış bağımlılık yok)
│   │   └── RealTimePoll.Application/     ← DTOs, Service Interfaces, Validators
│   ├── Infrastructure/
│   │   └── RealTimePoll.Infrastructure/  ← EF Core, Repository, Services (SMTP, JWT)
│   └── Presentation/
│       └── RealTimePoll.API/             ← Controllers, SignalR Hub, Middleware
└── RealTimePoll.sln
```

---

## ⚡ Hızlı Başlangıç

### 1. Gereksinimler
- .NET 8 SDK
- SQL Server (LocalDB / Express / Full)
- Visual Studio 2022 veya VS Code

### 2. appsettings.json Yapılandırması

`src/Presentation/RealTimePoll.API/appsettings.json` dosyasını düzenleyin:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=RealTimePollDb;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": "587",
    "Username": "your-gmail@gmail.com",
    "Password": "your-gmail-app-password"
  }
}
```

> **Gmail App Password için:** Google Hesabı → Güvenlik → 2FA Aç → Uygulama Şifreleri → Mail seç

### 3. Migration & Database

```bash
# Infrastructure klasöründe:
cd src/Infrastructure/RealTimePoll.Infrastructure

dotnet ef migrations add InitialCreate \
  --startup-project ../../Presentation/RealTimePoll.API \
  --output-dir Persistence/Migrations

dotnet ef database update \
  --startup-project ../../Presentation/RealTimePoll.API
```

### 4. Projeyi Çalıştır

```bash
cd src/Presentation/RealTimePoll.API
dotnet run
```

- **Swagger UI:** https://localhost:7001/swagger
- **SignalR Hub:** https://localhost:7001/hubs/poll

---

## 🔐 Varsayılan Admin Hesabı

```
Email: admin@realtimepoll.com
Şifre: Admin@123!
```

---

## 📡 API Endpoints

### Auth
| Method | Endpoint | Açıklama | Auth |
|--------|----------|----------|------|
| POST | /api/auth/register | Kayıt ol | ❌ |
| POST | /api/auth/login | Giriş yap | ❌ |
| POST | /api/auth/refresh-token | Token yenile | ❌ |
| POST | /api/auth/logout | Çıkış yap | ✅ |
| POST | /api/auth/forgot-password | Şifre sıfırlama maili | ❌ |
| POST | /api/auth/reset-password | Şifre sıfırla | ❌ |
| POST | /api/auth/change-password | Şifre değiştir | ✅ |
| GET  | /api/auth/confirm-email | Email onayla | ❌ |
| GET  | /api/auth/profile | Profili getir | ✅ |
| PUT  | /api/auth/profile | Profili güncelle | ✅ |

### Polls
| Method | Endpoint | Açıklama | Auth |
|--------|----------|----------|------|
| GET | /api/polls | Anketleri listele | ❌ |
| GET | /api/polls/{id} | Anket detayı | ❌ |
| GET | /api/polls/my | Kendi anketlerim | ✅ |
| POST | /api/polls | Anket oluştur | ✅ |
| PUT | /api/polls/{id} | Anket güncelle | ✅ |
| DELETE | /api/polls/{id} | Anket sil | ✅ |
| POST | /api/polls/{id}/activate | Aktif et | Admin |
| POST | /api/polls/{id}/close | Kapat | Admin |

### Votes
| Method | Endpoint | Açıklama | Auth |
|--------|----------|----------|------|
| POST | /api/votes | Oy kullan | ❌ (anonim) |
| GET | /api/votes/{pollId}/results | Sonuçları getir | ❌ |
| GET | /api/votes/{pollId}/has-voted | Oy kullandım mı? | ❌ |

### Admin
| Method | Endpoint | Açıklama | Auth |
|--------|----------|----------|------|
| GET | /api/admin/dashboard | Dashboard stats | Admin |
| GET | /api/admin/users | Kullanıcı listesi | Admin |
| PUT | /api/admin/users/{id}/toggle-status | Aktif/Pasif | Admin |
| POST | /api/admin/users/{id}/roles | Rol ata | SuperAdmin |
| DELETE | /api/admin/users/{id}/roles/{role} | Rol kaldır | SuperAdmin |
| GET | /api/admin/polls | Tüm anketler | Admin |

---

## 🔌 SignalR Real-Time Events

### Hub URL: `/hubs/poll`

#### Client → Server
```javascript
// Ankete bağlan
connection.invoke("JoinPoll", pollId);

// Anketten ayrıl
connection.invoke("LeavePoll", pollId);
```

#### Server → Client
```javascript
// Yeni oy geldiğinde
connection.on("VoteUpdate", (result) => { /* VoteResultResponse */ });

// Yeni anket oluşturulduğunda
connection.on("NewPollCreated", (poll) => { /* PollResponse */ });

// Anket güncellendiğinde
connection.on("PollUpdated", (poll) => { /* PollResponse */ });

// Anket durumu değiştiğinde
connection.on("PollStatusChanged", ({ pollId, status }) => { });
```

#### JavaScript SignalR Örneği:
```javascript
import * as signalR from "@microsoft/signalr";

const token = localStorage.getItem("accessToken");

const connection = new signalR.HubConnectionBuilder()
  .withUrl("https://localhost:7001/hubs/poll", {
    accessTokenFactory: () => token
  })
  .withAutomaticReconnect()
  .build();

await connection.start();
await connection.invoke("JoinPoll", "your-poll-id");

connection.on("VoteUpdate", (result) => {
  console.log("Real-time vote update:", result);
  // result = { pollId, pollTitle, totalVotes, results: [{optionId, optionText, voteCount, percentage}] }
});
```

---

## 📦 NuGet Paketleri

### Domain
- Sıfır bağımlılık ✅

### Application
- `AutoMapper` 13.0.1
- `FluentValidation` 11.9.2
- `FluentValidation.DependencyInjectionExtensions` 11.9.2
- `MediatR` 12.3.0

### Infrastructure
- `Microsoft.EntityFrameworkCore` 8.0.8
- `Microsoft.EntityFrameworkCore.SqlServer` 8.0.8
- `Microsoft.EntityFrameworkCore.Tools` 8.0.8
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 8.0.8
- `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.8
- `MailKit` 4.7.1.1
- `MimeKit` 4.7.1

### API
- `Microsoft.AspNetCore.SignalR` 1.1.0
- `Swashbuckle.AspNetCore` 6.7.3
- `Serilog.AspNetCore` 8.0.2
- `Serilog.Sinks.Console` 6.0.0
- `Serilog.Sinks.File` 6.0.0

---

## 🗄️ Veritabanı Tabloları

- `Users` — ASP.NET Identity kullanıcıları
- `Roles` — Admin, User, SuperAdmin rolleri
- `Polls` — Anketler
- `PollOptions` — Anket seçenekleri
- `Votes` — Oylar (anonim + kayıtlı)
- `RefreshTokens` — JWT refresh token'lar

---

## 🛡️ Güvenlik Özellikleri

- JWT Bearer Authentication (Access + Refresh Token)
- Refresh Token Rotation (kullanıldıktan sonra yenilenir)
- Password Reset via Email (15 dk geçerli)
- Email Confirmation
- Account Lockout (5 yanlış denemede 15 dk kilit)
- Soft Delete (veriler silinmez, işaretlenir)
- Global Exception Handler
- Anonymous voting (IP bazlı tekrar oy önleme)

---

## 🚀 Production Checklist

- [ ] `appsettings.json` → gerçek SMTP, connection string, JWT secret
- [ ] `RequireConfirmedEmail = true` (InfrastructureServiceRegistration.cs)
- [ ] HTTPS zorunlu kıl
- [ ] CORS origins güncelle
- [ ] Serilog → ElasticSearch / Seq ekle
- [ ] Rate limiting ekle (Microsoft.AspNetCore.RateLimiting)
- [ ] JWT Secret → Azure Key Vault / environment variable
