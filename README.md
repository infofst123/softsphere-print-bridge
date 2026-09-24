# Softsphere Print Bridge

Small Windows tray application that connects Odoo to printers installed in the current Windows user session.

## Why a tray application

A Windows Service runs in Session 0 and frequently cannot see printers installed only for a user. This application starts at user login, remains in the notification area, and prints through the same Windows queues visible to normal desktop applications.

## Features

- Select one Windows printer for tickets.
- Select a separate Windows printer/queue for the cash drawer.
- Print raw ESC/POS tickets through the Windows spooler.
- Open the drawer with the standard ESC/POS pulse command.
- Choose ticket-only or ticket-plus-drawer from Odoo.
- Authenticated HTTP API using `X-API-Key`.
- Automatic startup through the current user's Windows Run registry key.
- Test ticket and drawer buttons in the configuration window.

## API

Default listening URL: `https://127.0.0.1:17833`

The Odoo web client calls this loopback address from the cashier's browser. The
public Odoo server never needs network access to the Windows PC.

On first launch, the bridge creates a private localhost server certificate and
adds only its local root certificate to the current Windows user's trusted root
store. This avoids HTTPS mixed-content blocking in Chrome and Edge. Restart the
browser after the first bridge launch so it reloads the Windows trust store.

| Method | Endpoint | Authentication | Purpose |
|---|---|---|---|
| GET | `/api/health` | None | Service health |
| GET | `/api/printers` | `X-API-Key` | Installed printer names |
| POST | `/api/print-ticket` | `X-API-Key` | Print ticket and optionally open drawer |
| POST | `/api/open-cash-drawer` | `X-API-Key` | Open drawer only |

Example request:

```http
POST /api/print-ticket HTTP/1.1
Host: 192.168.1.12:17833
X-API-Key: YOUR_KEY
Content-Type: application/json

{
  "text": "VERDE\nSO/2026/001\nTotal TTC: 10.000 TND",
  "openCashDrawer": true
}
```

## Build and install

1. Publish a self-contained executable:

   ```powershell
   .\scripts\publish.ps1
   ```

2. Transfer and run
   `publish\win-x64\Softsphere.PrintBridge-Standalone-win-x64.exe` as the
   Windows user who owns the printers. Do not use the small executable from a
   `bin` directory; that development build requires an installed .NET runtime.
3. Choose both printer queues and save.
4. Copy the generated API key into Odoo Sales settings.
5. In Odoo, configure the bridge address as `127.0.0.1:17833`. Odoo assumes
   HTTPS when the protocol is omitted. This same value
   works on every cashier PC because each browser contacts its own computer.

Configuration is stored at `%LOCALAPPDATA%\Softsphere\PrintBridge\settings.json`.

## Security

- The bridge listens only on the Windows loopback interface.
- Every operational endpoint requires the generated API key.
- Do not expose or forward port 17833 to the LAN or Internet.
