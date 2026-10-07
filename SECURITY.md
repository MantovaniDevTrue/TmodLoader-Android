# Security policy

## Signing keys

Private APK signing keys must never be committed to this repository.

The local development builds used during bootstrap bring-up may use a disposable test key, but the private key must remain outside Git. The public certificate may be stored separately when needed for verification.

If a signing private key is accidentally committed, treat it as compromised and rotate it immediately.

## Runtime and game payloads

Do not commit proprietary Terraria/tModLoader runtime payloads, extracted game assets, account data, save files or personal logs containing private information.

## Bug reports

For bootstrap failures, prefer the sanitized diagnostic files listed in `docs/DEBUGGING.md`.
