# DOLCAD Real XML Notes

บันทึกนี้มาจากการดูไฟล์ตัวอย่างจริงในเครื่อง:

- `C:\Users\mennz\Desktop\แกะไฟล์dolcad\2014_21-01-2562 งานชั้น2.XML`
- `C:\Users\mennz\Desktop\แกะไฟล์dolcad\2014_21-01-2562 งานชั้น2_Decrypted.XML`
- `C:\Users\mennz\Desktop\แกะไฟล์dolcad\3002_17-02-2569 งานชั้น1.XML`
- `C:\Users\mennz\Desktop\แกะไฟล์dolcad\3002_17-02-2569 งานชั้น1_Decrypted.XML`

## รูปแบบไฟล์ที่เจอ

ไฟล์ `.XML` จริงเป็น encrypted:

```xml
<Root>
  <xs:schema>...</xs:schema>
  <TB_ENCRYPT>
    <data>...</data>
  </TB_ENCRYPT>
</Root>
```

ไฟล์ `_Decrypted.XML` เป็น plaintext:

```xml
<NewDataSet>
  <xs:schema>...</xs:schema>
  <TB_SVC_SURVEYDESC>...</TB_SVC_SURVEYDESC>
  ...
</NewDataSet>
```

## เงื่อนไขยืนยันว่าเป็น DOLCAD survey XML

Scout ควร copy เฉพาะไฟล์ที่ยืนยันได้ว่าเป็น DOLCAD survey XML:

- Plain XML: ต้องมี `TB_SVC_SURVEYDESC` และ `SURVEYJOB_NO`
- Encrypted XML: ต้องถอดรหัสได้ แล้วข้างในต้องมี `TB_SVC_SURVEYDESC` และ `SURVEYJOB_NO`

ดังนั้น XML อื่นที่เป็น config/help/template หรือเป็น `<NewDataSet>` แต่ไม่มี `TB_SVC_SURVEYDESC/SURVEYJOB_NO` ต้องถูก skip

## สรุปโครงสร้างจากไฟล์ตัวอย่าง

### งานชั้น 2: `2014_21-01-2562 งานชั้น2_Decrypted.XML`

Root: `NewDataSet`

ตารางหลักที่พบ:

| Table | Rows |
|---|---:|
| `TB_SVC_SURVEYDESC` | 1 |
| `TB_SVC_PARCELDESC` | 4 |
| `TB_SVC_PARCELMARK` | 78 |
| `TB_SVC_GRAPHIC` | 151 |
| `TB_SVC_BOUNDARY_UTM2` | 2 |
| `TB_SVC_BOUNDARYMARK_UTM2` | 5 |
| `TB_SVC_BENCHMARKOLD_UTM2` | 27 |
| `TB_SVC_MAINONLINE_UTM2` | 2 |
| `TB_SVC_ONLINE_UTM2` | 7 |
| `TB_SVC_TRAVERSECLOSE_UTM2` | 1 |
| `TB_SVC_TRAVERSECLOSEMARK_UTM2` | 8 |
| `TB_SVC_HEADDER_RW3K` | 1 |
| `TB_SVC_FEESURVEY_RW3K` | 1 |
| `TB_SVC_BKJ1` | 1 |
| `TB_SVC_PRINTDOL` | 1 |
| `TB_SVA_SURVEYREPORT` | 1 |

`TB_SVC_SURVEYDESC` มี field 56 ตัว และมี `SURVEYJOB_NO`

### งานชั้น 1: `3002_17-02-2569 งานชั้น1_Decrypted.XML`

Root: `NewDataSet`

ตารางหลักที่พบ:

| Table | Rows |
|---|---:|
| `TB_SVC_SURVEYDESC` | 1 |
| `TB_SVC_PARCELDESC` | 3 |
| `TB_SVC_PARCELMARK` | 53 |
| `TB_SVC_GRAPHIC` | 90 |
| `TB_SVC_BOUNDARY_UTM` | 2 |
| `TB_SVC_BOUNDARYMARK_UTM` | 18 |
| `TB_SVC_BENCHMARKOLD_UTM` | 6 |
| `TB_SVC_GPSMARKOLD_UTM` | 2 |
| `TB_SVC_HEADDER_RW3K` | 1 |
| `TB_SVC_FEESURVEY_RW3K` | 1 |
| `TB_SVC_PRINTDOL` | 1 |
| `TB_SVA_OWNER` | 2 |
| `TB_SVA_SURVEYREPORT` | 1 |

`TB_SVC_SURVEYDESC` มี field 61 ตัว และมี `SURVEYJOB_NO`

## ข้อสังเกตจากไฟล์จริง

- `งานชั้น1` ใช้ชุดตาราง `*_UTM`
- `งานชั้น2` ใช้ชุดตาราง `*_UTM2`
- `PROVINCE_NAME` ในตัวอย่างจริงว่าง แต่มี `PROVINCE_SEQ`
- `QUEUE_DATE` เป็น ค.ศ. ใน XML เช่น `2019-01-21T00:00:00+07:00` และ `2026-02-17T00:00:00+07:00`
- ชื่อไฟล์มี pattern ประมาณ `<รหัส>_<วัน-เดือน-ปี พ.ศ.> <ชนิดงาน>.XML`

## จุดค้นใน `docs/project_map.md`

ใช้ `project_map.md` เป็นดัชนีค้น source เดิมได้ตามนี้:

| ต้องการหาเรื่อง | คำค้น / ตำแหน่งใน project map |
|---|---|
| Encrypt/decrypt XML | `CLASS\ClassLibrary\Encryption.cs`, class `Encryption`, methods `EncryptFileToXml`, `DecryptFileFromXml`, `DecryptFileToStream`, `HasEncryptedData` |
| Export XML จาก DOLCAD | `CLASS\ImportExport\XML\ExpXML.cs`, class `ExpXML`, methods `ExportSurveyDesc`, `ExportParcelDesc`, `ExportParcelMark`, `ExportBoundaryUTM`, `ExportBoundaryUTM2`, `ExportGrapphic` |
| Import XML กลับ DB | `CLASS\ImportExport\XML\ImpXML.cs`, class `ImpXML`, methods `ImportDataFromXML`, `SaveSurveyDesc`, `SaveParcelDesc`, `SaveParcelMark`, `SaveGraphic` |
| Import XML แบบ DOL | `CLASS\ImportExport\XML\ImpXMLDOL.cs`, class `ImpXMLDOL`, methods `ImportSurveyDesc`, `ImportSurveyDescToDOLDB`, `importDOL`, `importds` |
| Import XML iCAD/SVM/LandOffice | `ImpXMLiCAD.cs`, `ImpXMLSVM.cs`, `ImpXMLLandOffice.cs` |
| Dataset schema ของ `TB_SVC_SURVEYDESC` | `XML\dsImpExpXML.cs`, class `dsImpExpXML`, `TB_SVC_SURVEYDESCDataTable`, `TB_SVC_SURVEYDESCRow` |
| UI setting folder export XML | `frmSettingSaveExportXML.cs` |
| UI import/export XML | ค้น `btnImportXML_Click`, `btnExportXML_Click`, `LoadXML`, `LoadXML_NEW` |

## สิ่งที่กระทบโปรแกรม Collector

- Filter ปัจจุบันที่ใช้ `TB_SVC_SURVEYDESC/SURVEYJOB_NO` เหมาะกับไฟล์จริงที่ตรวจแล้ว
- ถ้าต้องการ filter จังหวัดใน Manager ให้แม่นกับไฟล์จริง อาจต้องเพิ่ม `PROVINCE_SEQ` เพราะ `PROVINCE_NAME` ในไฟล์ตัวอย่างว่าง
- ถ้าต้องการแยกประเภทงานชั้น 1/2 อาจ parse จากชื่อไฟล์ หรือดู presence ของตาราง `*_UTM` กับ `*_UTM2`

