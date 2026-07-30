# Aegis Installer and Uninstaller

## 1. Installer goals

The installer should:
- deploy the service,
- set up configuration,
- initialize the database,
- deploy or guide installation of the browser extension,
- configure defaults,
- create integrity baselines,
- create secure keys,
- record versions.

## 2. Installation steps

1. Verify prerequisites.
2. Create install directories.
3. Write initial config.
4. Initialize SQLite.
5. Install or register the Windows Service.
6. Set up watchdog.
7. Install or register browser extension support.
8. Apply DNS/proxy configuration if enabled.
9. Record baseline integrity data.
10. Start protection.

## 3. Uninstaller goals

The uninstaller should:
- respect lock status,
- require the configured unlock workflow,
- reverse installer changes cleanly,
- restore configuration where appropriate,
- remove services and policies created by the app,
- leave no orphaned dependencies.

## 4. Normal uninstall flow

- user requests uninstall,
- system checks lock state,
- if locked, refuse and explain,
- if unlocked, begin staged confirmations,
- after completion, stop enforcement modules,
- remove files and metadata.

## 5. Rollback

If installation fails halfway through:
- undo partial changes,
- restore previous network settings if changed,
- remove partially deployed services,
- log the failure.

## 6. Supported behavior only

The installer and uninstaller should rely on supported Windows and browser mechanisms. No destructive or hidden behavior.

## 7. Recovery path

If something goes wrong:
- preserve the ability to restore the machine,
- avoid data loss,
- keep an audit trail.

## 8. Version upgrades

Upgrade flow should:
- verify existing install,
- migrate schema,
- preserve lock state,
- update policy packs,
- update extension/service versions safely.

## 9. Code signing, SmartScreen, and antivirus

### 9.1 Code signing

The installer executable and all deployed binaries (service, watchdog, UI) should be signed with an Authenticode code-signing certificate. This:
- prevents Windows SmartScreen from blocking the installer,
- builds reputation with Microsoft's SmartScreen scoring,
- allows antivirus products to whitelist the publisher.

For initial development, a self-signed certificate can be used for testing. For distribution, an EV or OV code-signing certificate is recommended.

### 9.2 SmartScreen

Windows SmartScreen will flag unsigned or low-reputation executables. Mitigations:
- Sign all binaries.
- Distribute via MSIX (MSIX packages are recognized by SmartScreen).
- Build reputation by accumulating downloads over time.
- Document for users: "You may see a SmartScreen warning on first install. Click 'More info' → 'Run anyway.'"

### 9.3 Antivirus considerations

Aegis exhibits behaviors commonly flagged by antivirus software:
- Installs a Windows Service.
- Modifies DNS settings.
- Writes browser registry policies.
- Monitors processes.
- Resists uninstallation.

Mitigations:
- Sign all binaries (reduces false positive rate significantly).
- Submit the installer to major AV vendors for whitelisting (Microsoft, Kaspersky, Norton, Malwarebytes).
- Document for users: "If your antivirus flags Aegis, add an exclusion for `%ProgramFiles%\Aegis\` and `%ProgramData%\Aegis\`."
- Never use obfuscation, packing, or anti-debugging techniques (these trigger heuristic detection).
