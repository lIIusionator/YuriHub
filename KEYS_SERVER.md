# YURI Keys Server Setup

## Overview
Keys are now served from a local Node.js server instead of being read directly from the file. This keeps `keys.json` hidden from git while remaining accessible to your application.

## Architecture

```
GitHub Repo (public)
    ↓
    ├─ server.js → Serves keys on localhost:3000
    ├─ keys.json (ignored in .gitignore) ← Local only
    └─ YURI_test.ahk → Fetches keys via HTTP
```

## Setup

### 1. Install Dependencies
```bash
npm install
```

### 2. Start Server
```bash
npm start
```

Server runs on: `http://localhost:3000`

### 3. API Endpoints

**Fetch all keys:**
```
GET http://localhost:3000/api/keys
```

Response:
```json
{
  "keys": [
    "8f3a9c7e2b1d4f5a9c3e7b2f5a8c1d4e",
    "a4d7e2c9f1b3e5a7c2f4d8b1e3a9c5f2",
    ...
  ]
}
```

**Health check:**
```
GET http://localhost:3000/health
```

### 4. Integrate with AutoHotkey

See `example_fetch_keys.ahk` for implementation details.

Key points for AHK integration:
- Use `WinHttp.WinHttpRequest` to make HTTP requests
- Parse JSON response with `JSON.Parse()` (AHK v2.0+)
- Access keys array: `keys.keys[1]` through `keys.keys[50]`

## Files

- `server.js` - Express server serving keys
- `package.json` - Node.js dependencies
- `keys.json` - Local only (not tracked in git)
- `.gitignore` - Excludes keys.json from version control
- `example_fetch_keys.ahk` - AutoHotkey example code

## Next Steps

1. ✅ Keys are hidden from git
2. ✅ Server ready to fetch keys
3. **TODO:** Integrate fetch code into YURI_test.ahk
4. **TODO:** Add device authentication/tokens (future)
5. **TODO:** Add encryption layer (future)
