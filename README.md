# coffeetime
<img src="https://img.shields.io/badge/Blazor-512BD4?style=for-the-badge&logo=blazor&logoColor=white"> <img src="https://img.shields.io/badge/MySQL-4479A1?style=for-the-badge&logo=mysql&logoColor=white">

커피는 맛있습니다.

<p align="center">
  <img width="70%" alt="Screenshot 2026-03-16 233620" src="https://github.com/user-attachments/assets/ff918d6d-b6ef-4fa1-8468-f2b74f703805" />
</p>

### Table of Contents
- [Features](#features)
- [Getting Started](#getting-started)
- [Showcase](#showcase)
  
---
### Features
- ADFS OpenID Connect sign-in/sign-out flow
- Bootstrap-based UI with modal-driven interactions
- Coffee beans management UI
- Package batch registration and tracking
- Windows container Dockerfile for deployment

---
### Getting Started
#### 1. Clone the repository
```
  git clone https://github.com/Coppermine-SP/coffeetime.git
  cd coffeetime/src
```
#### 2. Configure application settings
Create an `appsettings.json` file and mount it to `C:\confifg\appsettings.json` in the container.

```
{
  "ConnectionStrings": {
    "DefaultConnection": "server=localhost;port=3306;database=coffeetime;user=app;password=secret;"
  },
  "Authentication": {
    "Adfs": {
      "Authority": "https://id.example.com/adfs",
      "ClientId": "your-client-id",
      "ClientSecret": "your-client-secret",
      "ResourceId": "your-resource-id"
    }
  },
  "Proxy": {
    "AllowedProxy": "127.0.0.1"
  }
}
```
#### 3. Apply migrations to the database
> [!WARNING]
> **.NET CLI is required**
> 
> Please make sure that you have installed .NET CLI and ef-tools on your PC.
```
dotnet ef database update
```
#### 4. Running Container
The repository includes a Windows container Dockerfile targeting .NET 10 Nano Server.

**Build**
```
docker build -t coffeetime ./src
```

**Run**
```
docker run -p 8080:8080 coffeetime
```
---
### Showcase

**SSO sign-in page**

<img width="1606" height="869" alt="signin" src="https://github.com/user-attachments/assets/3576ff8d-92ee-402b-9345-f92e9e6b5c90" />

**Dashboard**

<img width="1588" height="1025" alt="dashboard" src="https://github.com/user-attachments/assets/939a1b8e-62f2-4158-94bd-d9c7da690254" />

**Transactions**

<img width="1607" height="869" alt="transactions" src="https://github.com/user-attachments/assets/1502b845-1564-44ac-9aea-bb05fc9cae24" />

**Batches**

<img width="1607" height="869" alt="batches" src="https://github.com/user-attachments/assets/0a7f8263-d65f-42bc-8449-5b52cb573ad2" />

