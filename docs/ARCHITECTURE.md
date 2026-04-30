# MSI XML Collector — Architecture

## Solution Structure

```
MSI_XML_Collector.sln
├── Scout/          (Console App — runs on USB, collects files)
└── Manager/        (WPF App — dashboard, review, import)
```

## USB Layout

```
Scout_XML.exe
secrets.key          ← AES key (NOT in git, NOT in DB)
Master_Index.db      ← SQLite metadata store
Collected_XMLs\
  [MachineName]\
    [relative path to original file]
```

---

## XML Formats

### Plaintext (เวอร์ชันเก่า)
```xml
<NewDataSet>
  <TB_SVC_SURVEYDESC>
    <SURVEYJOB_NO>389/2569</SURVEYJOB_NO>
    <OWNER_NAME>...</OWNER_NAME>
    <QUEUE_DATE>2026-02-25T00:00:00+07:00</QUEUE_DATE>
    <PROVINCE_NAME>บุรีรัมย์</PROVINCE_NAME>
    ...
  </TB_SVC_SURVEYDESC>
</NewDataSet>
```

### Encrypted (เวอร์ชันใหม่)
```xml
<Root>
  <TB_ENCRYPT>
    <data>Base64(IV[16 bytes] + AES-256-CBC ciphertext)</data>
  </TB_ENCRYPT>
</Root>
```
Decrypted payload = plaintext XML with same `<NewDataSet>` structure.

---

## Scout Pipeline (Phase 2)

```
*.xml file on disk
   │
   ▼
XmlIdentifier.Detect(bytes)
   ├─ root=<Root>       → Encrypted
   ├─ root=<NewDataSet> → Plaintext
   └─ other            → Unknown → skip + log
   │
   ▼ (if Encrypted)
AesDecryptor.DecryptXml(bytes)
   └─ loads secrets.key (16/24/32 bytes raw binary)
   └─ extracts TB_ENCRYPT/data (Base64)
   └─ AES-256-CBC: first 16 bytes = IV, rest = ciphertext
   └─ returns plaintext bytes IN MEMORY ONLY
   │
   ▼
MD5.HashData(plaintextBytes)  → FileHash (hex string)
   │
   ▼
FileRecordRepository.ExistsByHash(hash, machineName)
   └─ duplicate? → [DUP] log → skip
   │
   ▼
XmlParser.Parse(plaintextBytes)
   └─ picks first TB_SVC_SURVEYDESC row
   └─ returns SurveyFields
   │
   ▼
FileRecordRepository.Upsert(FileRecord)
   │
   ▼
Copy original file (encrypted or plain) to:
  Collected_XMLs\[MachineName]\[relative path]
```

---

## SQLite Schema — Files table

| Column           | Type    | Notes                                    |
|------------------|---------|------------------------------------------|
| Id               | INTEGER | PK autoincrement                         |
| FileHash         | TEXT    | MD5 of **plaintext** bytes (hex)         |
| OriginalFileName | TEXT    |                                          |
| OriginalPath     | TEXT    | Full path on source machine              |
| MachineName      | TEXT    | `Environment.MachineName`                |
| FileSize         | INTEGER | Bytes (of original/raw file)             |
| IsEncrypted      | INTEGER | 0 = plaintext, 1 = encrypted             |
| LastWriteTime    | TEXT    | ISO-8601 UTC                             |
| CollectedAt      | TEXT    | ISO-8601 UTC, set at collection time     |
| SurveyJobNo      | TEXT    | From TB_SVC_SURVEYDESC.SURVEYJOB_NO      |
| OwnerName        | TEXT    | OWNER_NAME                               |
| QueueDate        | TEXT    | QUEUE_DATE                               |
| ProvinceName     | TEXT    | PROVINCE_NAME                            |
| AmphurSeq        | INTEGER | AMPHUR_SEQ                               |
| TambolSeq        | INTEGER | TAMBOL_SEQ                               |
| LandNo           | INTEGER | LAND_NO                                  |
| SurveyNo         | INTEGER | SURVEY_NO                                |
| SurveyorName     | TEXT    | SURVEYOR_NAME                            |

**Unique index:** `(FileHash, MachineName)` — same file from different machines = separate rows.

---

## AES Key Format

`secrets.key` = raw binary file, **16, 24, or 32 bytes** (AES-128 / AES-192 / AES-256).  
No header, no encoding — pure key bytes.

---

## Scout CLI

```
Scout_XML.exe --source <folder> [--key <path>] [--db <path>]

Defaults:
  --key   → secrets.key  (next to exe)
  --db    → Master_Index.db (next to exe)
```

Exit codes: `0` = success, `1` = bad args, `2` = one or more file errors.

---

## Phases

| Phase | Status | Description |
|-------|--------|-------------|
| 1 | ✅ Done | SQLite schema + repository |
| 2 | ✅ Done | Scout pipeline (identify → decrypt → hash → parse → save → copy) |
| 3 | 🔲 Todo | WPF Manager (DataGrid, preview, conflict, bulk import, DB merge) |
| 4 | 🔲 Todo | Integration tests + edge cases |

---

## Edge Cases (Phase 4)

- `secrets.key` missing / wrong key → clear error, no crash
- Corrupt XML → skip + log, continue to next file
- USB disk full → IOException caught, logged as error
- Duplicate machine name → handled by `(FileHash, MachineName)` unique index
- Same file copied from two machines → two separate rows in DB
- File with no TB_SVC_SURVEYDESC data → row saved with null parsed fields
