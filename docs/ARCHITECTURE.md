# MSI XML Collector — Architecture

## ภาพรวมระบบ

รวบรวมไฟล์ XML ของงานสำรวจที่ดิน (DOL) จากหลายเครื่องผ่าน USB  
ใช้ MD5 hash คุม deduplication, รองรับ XML แบบ plain และเข้ารหัส AES

```
[เครื่องสนาม A] ──┐
[เครื่องสนาม B] ──┼── Scout_XML.exe ──→ USB ──→ Manager.exe ──→ Master DB
[เครื่องสนาม C] ──┘
```

---

## Solution Structure

```
MSI_XML_Collector/
├── Scout/          Console App — รันบน USB, เก็บไฟล์จากเครื่องสนาม
└── Manager/        WPF App — dashboard, filter, preview, merge DB
```

---

## โครงสร้าง USB

```
Scout_XML.exe
secrets.key          ← 48-byte AES key (ไม่อยู่ใน git)
Master_Index.db      ← SQLite metadata store (ไม่อยู่ใน git)
Collected_XMLs\
  [MachineName]\
    [relative path จาก drive root]
```

---

## XML สองรูปแบบ

### Plaintext (เวอร์ชันเก่า)
```xml
<NewDataSet>
  <TB_SVC_SURVEYDESC>
    <SURVEYJOB_NO>389/2569</SURVEYJOB_NO>
    <OWNER_NAME>นายบุญร่วม แก้วสมนึก</OWNER_NAME>
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
    <data>Base64(AES-256-CBC ciphertext)</data>
  </TB_ENCRYPT>
</Root>
```
Decrypted payload = plaintext XML ที่มีโครงสร้าง `<NewDataSet>` เหมือนกัน  
IV **ไม่ได้ prepend** ใน ciphertext — IV มาจาก `secrets.key` bytes [32..47]

---

## secrets.key — รูปแบบและวิธีสร้าง

`secrets.key` = raw binary file, **48 bytes** แน่นอน:

| Bytes   | ความหมาย                          |
|---------|-----------------------------------|
| [0..31] | AES-256 key (SHA-256 ของ passphrase) |
| [32..47]| AES IV (MD5 ของ passphrase)          |

passphrase มาจากระบบ MSI ต้นฉบับ (com.dol.samart.svc) — เก็บใน password manager ขององค์กร ไม่บันทึกใน repo

**วิธีสร้าง secrets.key (PowerShell):**
```powershell
$p     = "<passphrase>"
$bytes = [Text.Encoding]::UTF8.GetBytes($p)
$key   = [Security.Cryptography.SHA256]::Create().ComputeHash($bytes)  # 32 bytes
$iv    = [Security.Cryptography.MD5]::Create().ComputeHash($bytes)     # 16 bytes
[IO.File]::WriteAllBytes("secrets.key", ($key + $iv))                  # 48 bytes total
```

---

## Scout Pipeline

```
Drive auto-discovery
   ├─ C:\  → DOLCAD_XML, Users\*\Desktop, Users\*\Documents เท่านั้น
   └─ D:\, E:\, ... (Fixed, ไม่ใช่ drive ที่ exe รันอยู่) → สแกนทั้ง drive
   │
   ▼
SafeEnumerateXml (*.xml, recursive, ข้าม access-denied โดยไม่หยุด)
   │
   ▼
XmlIdentifier.Detect(bytes)
   ├─ root=<Root>       → Encrypted
   ├─ root=<NewDataSet> → Plaintext
   └─ อื่นๆ            → Unknown → [SKIP]
   │
   ▼ (ถ้า Encrypted)
AesDecryptor.DecryptXml(bytes)
   └─ key = secrets.key[0..31], IV = secrets.key[32..47]
   └─ Base64 decode → AES-256-CBC decrypt → plaintext bytes (in memory only)
   │
   ▼
MD5.HashData(plaintextBytes) → FileHash (hex)
   │
   ├─ ExistsByHash(hash, machineName) → ซ้ำ? → [DUP] skip
   │
   ▼
XmlParser.Parse(plaintextBytes) → SurveyFields (จาก TB_SVC_SURVEYDESC แถวแรก)
   │
   ▼
FileRecordRepository.Upsert(FileRecord) → บันทึกลง SQLite
   │
   ▼
Copy ไฟล์ต้นฉบับ (encrypted หรือ plain) →
  Collected_XMLs\[MachineName]\[relative path จาก drive root]
```

---

## Scout CLI

```
Scout_XML.exe [--source <folder>] [--key <path>] [--db <path>]

ค่าเริ่มต้น:
  --key  → secrets.key  (ข้างๆ exe)
  --db   → Master_Index.db (ข้างๆ exe)

ไม่มี --source = Auto mode (scan ตาม drive rules ด้านบน)
มี --source    = Manual mode (scan เฉพาะโฟลเดอร์นั้น — ใช้ทดสอบ)

Exit codes:
  0 = สำเร็จทุกไฟล์
  1 = arguments ผิดพลาด
  2 = มี error อย่างน้อย 1 ไฟล์
```

---

## SQLite Schema — Files table

| Column           | Type    | หมายเหตุ                              |
|------------------|---------|---------------------------------------|
| Id               | INTEGER | PK autoincrement                      |
| FileHash         | TEXT    | MD5 ของ **plaintext** bytes (hex)     |
| OriginalFileName | TEXT    |                                       |
| OriginalPath     | TEXT    | Full path บนเครื่องต้นทาง            |
| MachineName      | TEXT    | `Environment.MachineName`             |
| FileSize         | INTEGER | bytes ของไฟล์ต้นฉบับ (raw)           |
| IsEncrypted      | INTEGER | 0 = plain, 1 = encrypted              |
| LastWriteTime    | TEXT    | ISO-8601 UTC                          |
| CollectedAt      | TEXT    | ISO-8601 UTC                          |
| SurveyJobNo      | TEXT    | SURVEYJOB_NO                          |
| OwnerName        | TEXT    | OWNER_NAME                            |
| QueueDate        | TEXT    | QUEUE_DATE                            |
| ProvinceName     | TEXT    | PROVINCE_NAME                         |
| AmphurSeq        | INTEGER | AMPHUR_SEQ                            |
| TambolSeq        | INTEGER | TAMBOL_SEQ                            |
| LandNo           | INTEGER | LAND_NO                               |
| SurveyNo         | INTEGER | SURVEY_NO                             |
| SurveyorName     | TEXT    | SURVEYOR_NAME                         |

**Unique index:** `(FileHash, MachineName)` — ไฟล์เดียวกันจากคนละเครื่อง = คนละ row

---

## Manager Features

| Feature           | รายละเอียด                                                    |
|-------------------|---------------------------------------------------------------|
| Open DB           | เปิด `Master_Index.db` จาก USB หรือ local                    |
| DataGrid          | แสดงทุก record, sort ได้ทุก column                           |
| Filter            | กรองตามจังหวัด / เครื่อง / ค้นหา text / เฉพาะไฟล์ซ้ำ       |
| Conflict detection| แถวที่มี SurveyJobNo ซ้ำกัน → พื้นหลังสีเหลือง              |
| Preview           | แสดง metadata + อ่าน XML จาก `Collected_XMLs\` อัตโนมัติ   |
| Merge             | merge records จาก DB อื่น (USB ที่ 2) เข้า DB ปัจจุบัน      |

---

## Integration Test Results

ทดสอบด้วยไฟล์จริงใน `docs/`:

| ไฟล์                    | รูปแบบ    | ผล                          |
|-------------------------|-----------|------------------------------|
| `2011_25-02-2569.XML`   | Plaintext | ✅ `[OK] [PLAIN] 389/2569`  |
| `1006_25-11-2563.XML`   | Encrypted | ✅ `[OK] [ENC] 294/2564`    |

| Scenario               | ผล                                          |
|------------------------|----------------------------------------------|
| secrets.key missing    | `[WARN]` + ต่อ, encrypted file → `[ERROR]`  |
| secrets.key ถูกต้อง   | decrypt + parse ครบ                          |
| รันซ้ำไฟล์เดิม        | `[DUP]` — ไม่บันทึกซ้ำ                      |
| โฟลเดอร์ access denied | ข้ามโดยไม่ crash                             |

---

## สถานะการพัฒนา

| Phase | Status | คำอธิบาย |
|-------|--------|-----------|
| 1 | ✅ | SQLite schema + repository |
| 2 | ✅ | Scout pipeline (identify → decrypt → hash → parse → save → copy) |
| 3 | ✅ | WPF Manager (DataGrid, filter, preview, conflict, merge) |
| 4 | ✅ | Integration tests + edge cases |
| 5 | ✅ | Scout scanning scope — C: specific folders only, other drives full scan |
