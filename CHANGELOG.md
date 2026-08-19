# Changelog
## 0.5.0
- Added Workshop companion terminal UI.
- Added secure client-to-server grant/revoke requests.
- Changed default minimum reputation from 1500 to 500.
- Retained explicit grants rather than automatic sharing.
- Retained world-AABB nearby-grid detection fix.
- Retained current `MyTerminalBlock.HasPlayerAccess(long, relation)` signature fix.
- Uses Harmony positional parameters to avoid `playerId` vs `identityId` breakage.
- Access patches are now non-fatal and log missing targets.
- Added `GetUserRelationToOwner` relation override path and optional debug access logging.
- Kept chat commands as fallback/admin interface.
