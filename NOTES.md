# บันทึกงาน — MSI XML Collector

> ถ้าอยากรู้โครงสร้างโปรเจกต์ ไฟล์ไหนทำอะไร อยู่ตรงไหน → อ่าน [project_map.md](project_map.md)
> ถ้าอยากดูขั้นตอนใช้งานจริงตั้งแต่ USB ถึงเครื่องแม่ → อ่าน [docs/WORKFLOW.md](docs/WORKFLOW.md)
> ถ้าอยากดูโครงสร้าง XML จริงและจุดค้น source เดิม → อ่าน [docs/DOLCAD_REAL_XML_NOTES.md](docs/DOLCAD_REAL_XML_NOTES.md)

## ทำแล้ว ✅

### Scout (Console App — รันบน USB)
- สแกน XML จากเครื่องสนาม + dedup ด้วย MD5
- รองรับ XML 2 แบบ: plain (`<NewDataSet>`) และ encrypted (`<Root>/<TB_ENCRYPT>`)
- ถอดรหัส AES-256-CBC ใน memory ไม่เขียนไฟล์ทิ้ง
- Copy เฉพาะ XML ที่ยืนยันว่าเป็น DOLCAD survey XML ได้ (`TB_SVC_SURVEYDESC` + `SURVEYJOB_NO`)
- ถ้าไม่มี `secrets.key`: ไฟล์ encrypted จะถูก skip เพราะยืนยันเนื้อหา DOLCAD ไม่ได้
- Auto mode: C: สแกนเฉพาะ `DOLCAD_XML`, `Desktop`, `Documents`
- Drive อื่น (D, E, F...): สแกนทั้ง drive ยกเว้น drive ที่ exe รันอยู่
- ข้ามโฟลเดอร์ระบบ/backup เช่น `Windows`, `Windows.old`, `Program Files`, `System Volume Information`, `$Recycle.Bin`, `Recovery`
- `SafeEnumerateXml`: ข้าม access-denied โดยไม่ crash
- แสดง progress ระหว่างสแกน และเขียน log file ไว้ที่ `Logs\Scout_yyyyMMdd_HHmmss.log`
- มี `--help`, ตรวจ argument ผิด, `--pause`, และ `Run_Scout.bat` สำหรับดับเบิลคลิกบน USB
- บันทึกลง SQLite (`Master_Index.db`)
- Copy ไฟล์ต้นฉบับ → `Collected_XMLs\[MachineName]\[path]`
- Publish แบบ self-contained win-x64 แล้วที่ `publish\Scout_USB\`

### Manager (WPF — เครื่องแม่)
- เปิด DB → DataGrid แสดงทุก record
- Filter: จังหวัด / เครื่อง / ค้นหา / เฉพาะไฟล์ซ้ำ
- Conflict detection: เลขที่สอบสวนซ้ำ → แถวสีเหลือง
- Preview: คลิกแถว → ดู metadata + เนื้อหา XML
- Preview ไฟล์ encrypted ใน Manager ได้ถ้ามี `secrets.key` ข้าง DB หรือข้าง Manager.exe
- Merge จาก USB:
  - merge DB records (upsert)
  - copy ไฟล์ XML จาก USB → เครื่องแม่
  - ถามยืนยันก่อนลบ `Collected_XMLs` บน USB (เก็บ DB ไว้)
- Merge ทำงานแบบ async พร้อม progress bar/status ไม่ค้าง UI
- First-run: ยังไม่มี DB → สร้างใหม่ผ่าน SaveFileDialog
- Export CSV จากรายการที่กำลังแสดงใน DataGrid (ตาม filter ปัจจุบัน)
- Publish แบบ self-contained win-x64 แล้วที่ `publish\Manager_PC\Manager.exe`

### secrets.key
- สร้างแล้ว (48 bytes = SHA256 + MD5 ของ passphrase)
- เก็บไว้ที่ Desktop
- passphrase บันทึกใน Claude memory ของโปรเจกต์นี้

---

## ยังไม่ได้ทำ ❌

### Scout
- ยังไม่ได้ทดสอบ Auto mode กับเครื่องจริง (ใช้ `--source` ทดสอบอยู่)
- ยังไม่มี timeout ตัดการสแกน drive ใหญ่แบบบังคับ (ตอนนี้มี progress/log แล้ว)

### Manager
- ยังไม่มี Export เป็น Excel (`.xlsx`) โดยตรง — ตอนนี้มี CSV แล้ว
- ไม่มีระบบ resolve conflict (ตอนนี้แค่ highlight เหลือง ไม่มี action)

### ทั่วไป
- ยังไม่มี installer แบบ setup wizard (ตอนนี้มี publish folder สำหรับก๊อปลง USB แล้ว)
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
