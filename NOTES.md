# บันทึกงาน — MSI XML Collector

## ทำแล้ว ✅

### Scout (Console App — รันบน USB)
- สแกน XML จากเครื่องสนาม + dedup ด้วย MD5
- รองรับ XML 2 แบบ: plain (`<NewDataSet>`) และ encrypted (`<Root>/<TB_ENCRYPT>`)
- ถอดรหัส AES-256-CBC ใน memory ไม่เขียนไฟล์ทิ้ง
- ถ้าไม่มี `secrets.key`: ไฟล์ encrypted ยังเก็บได้ แต่ fields ว่าง
- Auto mode: C: สแกนเฉพาะ `DOLCAD_XML`, `Desktop`, `Documents`
- Drive อื่น (D, E, F...): สแกนทั้ง drive ยกเว้น drive ที่ exe รันอยู่
- `SafeEnumerateXml`: ข้าม access-denied โดยไม่ crash
- บันทึกลง SQLite (`Master_Index.db`)
- Copy ไฟล์ต้นฉบับ → `Collected_XMLs\[MachineName]\[path]`

### Manager (WPF — เครื่องแม่)
- เปิด DB → DataGrid แสดงทุก record
- Filter: จังหวัด / เครื่อง / ค้นหา / เฉพาะไฟล์ซ้ำ
- Conflict detection: เลขที่สอบสวนซ้ำ → แถวสีเหลือง
- Preview: คลิกแถว → ดู metadata + เนื้อหา XML
- Merge จาก USB:
  - merge DB records (upsert)
  - copy ไฟล์ XML จาก USB → เครื่องแม่
  - ถามยืนยันก่อนลบ `Collected_XMLs` บน USB (เก็บ DB ไว้)
- First-run: ยังไม่มี DB → สร้างใหม่ผ่าน SaveFileDialog

### secrets.key
- สร้างแล้ว (48 bytes = SHA256 + MD5 ของ passphrase)
- เก็บไว้ที่ Desktop
- passphrase บันทึกใน Claude memory ของโปรเจกต์นี้

---

## ยังไม่ได้ทำ ❌

### Scout
- ยังไม่ได้ทดสอบ Auto mode กับเครื่องจริง (ใช้ `--source` ทดสอบอยู่)
- ยังไม่มี timeout / progress แจ้งเมื่อสแกน drive ใหญ่นานๆ
- ไม่มีการ log ไฟล์ (เขียน log file ทิ้งไว้) — ตอนนี้ print console อย่างเดียว

### Manager
- ไม่มี async / progress bar ตอน Merge ไฟล์เยอะ UI จะค้าง
- ไม่มีปุ่ม Export (export รายการเป็น Excel/CSV)
- ไม่มีการ preview ไฟล์ encrypted (แสดง raw ciphertext ไม่ได้ถอดรหัสใน Manager)
- ไม่มีระบบ resolve conflict (ตอนนี้แค่ highlight เหลือง ไม่มี action)

### ทั่วไป
- ยังไม่มี installer / deploy package สำหรับ USB
- ยังไม่ได้ทดสอบกับ USB จริงในสนาม

---

## ยังไม่เข้าใจ / ต้องถาม ❓

### เรื่อง DOLCAD XML
1. **ชื่อไฟล์** เช่น `2005_03-10-2568.XML`
   - `2005` คืออะไร? รหัสสำนักงาน? รหัสเขต? รหัสอำเภอ?
   - format วันที่ `03-10-2568` = วัน-เดือน-ปี พ.ศ. ใช่ไหม?

2. **โครงสร้างโฟลเดอร์จริง** ใน `C:\DOLCAD_XML`
   - มีโฟลเดอร์ย่อยแบบไหน? แยกตามปี? ตามเขต?

3. **เวอร์ชัน encrypted vs plain**
   - เครื่องเก่าทั้งหมด plain หรือมีทั้งสองแบบปนกัน?
   - มีวิธีรู้ว่าเครื่องไหนใช้เวอร์ชันไหน?

4. **ไฟล์ `Decrypted_*.XML`** ที่เจอในโฟลเดอร์ทดสอบ
   - ระบบ DOLCAD สร้างเองหรือใครสร้าง?
   - ควร collect ไหม หรือควร skip?

5. **ไฟล์ XML อื่นๆ ใน DOLCAD** (help, template, config)
   - ควร skip ไฟล์ที่ไม่มี `SURVEYJOB_NO` หรือเก็บไว้ก็ไม่เป็นไร?

### เรื่อง workflow จริงในสนาม
6. นักสำรวจใช้ DOLCAD แล้วไฟล์ XML ถูกสร้างตอนไหน / เก็บที่ไหนบ้าง?
7. USB เสียบเครื่องสนาม → Scout รัน → ถอด USB → เอามาเครื่องแม่
   - ใครรัน Scout? นักสำรวจ หรือ IT?
   - รันตอนไหน? ทุกวัน? ทุกสัปดาห์?
