# MSI XML Collector

ดูขั้นตอนใช้งานจริงทั้งหมดได้ที่ [WORKFLOW.md](WORKFLOW.md)  
ดูบันทึกโครงสร้าง XML จริงและจุดค้น source เดิมได้ที่ [DOLCAD_REAL_XML_NOTES.md](DOLCAD_REAL_XML_NOTES.md)

## วัตถุประสงค์
รวบรวมไฟล์ XML งานสำรวจที่ดิน (DOL) จากหลายเครื่องผ่าน USB  
deduplication ด้วย MD5, รองรับ XML แบบ plain และเข้ารหัส AES

---

## โครงสร้าง USB

```
Scout_XML.exe       ← รันตรงนี้
Run_Scout.bat       ← ดับเบิลคลิกไฟล์นี้ได้ หน้าต่างจะ pause ตอนจบ
secrets.key         ← AES key 48 bytes (สร้างจาก passphrase)
Master_Index.db     ← SQLite (สร้างอัตโนมัติ)
Collected_XMLs\     ← ไฟล์ที่เก็บได้ (สร้างอัตโนมัติ)
```

---

## การใช้งาน Scout

**ปกติ (Auto mode) — รันแล้วจบ:**
```
Scout_XML.exe
```
หรือดับเบิลคลิก:
```
Run_Scout.bat
```
สแกนอัตโนมัติ:
- `C:\DOLCAD_XML`
- `C:\Users\*\Desktop`
- `C:\Users\*\Documents`
- Drive อื่น (D:, E:, ...) ทั้ง drive

ระหว่างสแกน drive อื่น Scout จะข้ามโฟลเดอร์ระบบ/backup เช่น `Windows`, `Windows.old`, `Program Files`, `System Volume Information`, `$Recycle.Bin`, `Recovery` เพื่อไม่เข้าไปค้น Windows อีกชุดหรือไฟล์ระบบที่ไม่เกี่ยวกับ DOLCAD

**ทดสอบ (Manual mode):**
```
Scout_XML.exe --source C:\MSI\TestData
```

ดู help:
```
Scout_XML.exe --help
```

ระหว่างรัน Scout จะแสดง progress ใน console และเขียน log ไว้ที่:
```
Logs\Scout_yyyyMMdd_HHmmss.log
```

ไฟล์ที่ publish พร้อมก๊อปลง USB อยู่ที่:
```
publish\Scout_USB\
```

---

## secrets.key

ไฟล์ binary 48 bytes — derive จาก passphrase ของระบบ MSI  
ถ้าไม่มี: ไฟล์ plain XML ยังทำงานได้ปกติ, ไฟล์ encrypted จะถูก skip เพราะ Scout ยืนยันไม่ได้ว่าเป็น DOLCAD survey XML จริง

Scout จะ copy เฉพาะ XML ที่ยืนยันว่าเป็นงาน DOLCAD ได้เท่านั้น:
- plain XML ต้องมี `TB_SVC_SURVEYDESC` และ `SURVEYJOB_NO`
- encrypted XML ต้องถอดรหัสได้ แล้วพบ `TB_SVC_SURVEYDESC` และ `SURVEYJOB_NO`

---

## Manager

เปิด `Manager.exe` → เปิด DB → ดูข้อมูล, filter, merge จาก USB อื่น

---

## ไฟล์ตัวอย่าง (docs/)
- `2011_25-02-2569.XML` — Plaintext format
- `1006_25-11-2563.XML` — Encrypted format
