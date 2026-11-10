# Security policy

## Signing keys

Private APK signing keys must never be committed to this repository.

Release APKs are signed with a private key kept outside Git. Android only installs an update signed with the same key, so do not re-sign a release with a different key. The public certificate may be stored separately when needed for verification.

If a signing private key is accidentally committed, treat it as compromised and rotate it immediately.

## Runtime and game payloads

Do not commit proprietary Terraria/tModLoader runtime payloads, extracted game assets, account data, save files or personal logs containing private information.

## Bug reports

Attach the report exported from the launcher's Diagnostics page. Check it for personal paths or account names before posting it publicly.
