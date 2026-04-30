# MSI XML Collector

## วัตถุประสงค์
รวบรวมไฟล์ XML งานสำรวจที่ดิน (DOL) จากหลายเครื่องผ่าน USB  
deduplication ด้วย MD5, รองรับ XML แบบ plain และเข้ารหัส AES

---

## โครงสร้าง USB

```
Scout_XML.exe       ← รันตรงนี้
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
สแกนอัตโนมัติ:
- `C:\DOLCAD_XML`
- `C:\Users\*\Desktop`
- `C:\Users\*\Documents`
- Drive อื่น (D:, E:, ...) ทั้ง drive

**ทดสอบ (Manual mode):**
```
Scout_XML.exe --source C:\MSI\TestData
```

---

## secrets.key

ไฟล์ binary 48 bytes — derive จาก passphrase ของระบบ MSI  
ถ้าไม่มี: ไฟล์ plain XML ยังทำงานได้ปกติ, ไฟล์ encrypted จะ `[ERROR]`

---

## Manager

เปิด `Manager.exe` → เปิด DB → ดูข้อมูล, filter, merge จาก USB อื่น

---

## ไฟล์ตัวอย่าง (docs/)
- `2011_25-02-2569.XML` — Plaintext format
- `1006_25-11-2563.XML` — Encrypted format
