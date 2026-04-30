# MSI XML Collector

## วัตถุประสงค์
รวบรวมไฟล์ .xml จากคอมพิวเตอร์หลายเครื่องผ่าน USB
โดยใช้ MD5 Hash คุม deduplication และรองรับ XML ทั้งแบบ plain และเข้ารหัส AES

## โครงสร้าง USB
Scout_XML.exe | secrets.key | Master_Index.db | Collected_XMLs\

## XML สองรูปแบบ
- **Encrypted** (เวอร์ชันใหม่): root = `<Root>` / table = `<TB_ENCRYPT>` / ข้อมูลใน `<data>` เป็น Base64+AES
- **Plaintext** (เวอร์ชันเก่า): root = `<NewDataSet>` / table = `<TB_SVC_SURVEYDESC>`

## Key Management
AES key อ่านจาก `secrets.key` ข้างๆ Scout_XML.exe บน USB
ถอดรหัส in-memory เท่านั้น ไม่เขียนไฟล์ทิ้ง
**secrets.key และ *.db อยู่ใน .gitignore**

## Projects
- **Scout** (Console App): scan → identify → decrypt → hash → copy
- **Manager** (WPF): dashboard → preview → conflict → import

## Field หลักจาก TB_SVC_SURVEYDESC
SURVEYJOB_NO, OWNER_NAME, QUEUE_DATE, PROVINCE_NAME,
AMPHUR_SEQ, TAMBOL_SEQ, LAND_NO, SURVEY_NO, SURVEYOR_NAME