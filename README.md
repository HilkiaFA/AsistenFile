# Asisten File & Dokumen Lokal (On-Device RAG AI)

Aplikasi desktop profesional berbasis **C# .NET 8 Windows Forms** untuk mengindeks seluruh dokumen di komputer Anda (PDF, Word, Excel, CSV, Catatan/Markdown, Email EML, dan Scan/Foto OCR), lalu menjawab pertanyaan dalam **bahasa natural** secara **100% lokal on-device** lengkap dengan sitasi file sumber dan nomor halamannya.

Aplikasi ini dirancang khusus untuk **freelancer, kantor kecil, konsultan, akuntan, dan profesional yang peduli kerahasiaan data**. Data tidak pernah keluar dari komputer Anda, tetap berfungsi tanpa koneksi internet, dan setiap jawaban selalu faktual sesuai isi dokumen asli (bebas halusinasi).

---

## Fitur Utama & Keunggulan

### 1. Tanya & Chat Dokumen Bahasa Alami (RAG Lokal)
- Pengguna cukup mengetik pertanyaan dalam bahasa sehari-hari:
  - *“cari kontrak sewa yang saya tanda tangani tahun lalu”*
  - *“rangkum semua invoice bulan Maret”*
  - *“apa kewajiban penyewa dalam perjanjian sewa ruko?”*
- Otomatis mengekstrak filter rentang waktu (*“tahun lalu”*, *“bulan lalu”*, *“Maret 2026”*), ekstensi file, dan kata kunci relevan.
- Setiap jawaban dilengkapi **Kartu Sitasi Sumber** yang menampilkan:
  - Nama file & nomor halaman / lembar sheet.
  - Skor relevansi & cuplikan teks asli.
  - Tombol **" Buka File"** untuk membuka langsung dokumen di aplikasi default komputer (Adobe Acrobat, Word, Excel, dll).
  - Tombol **" Buka Folder"** untuk membuka Windows Explorer pada lokasi file.
- Riwayat percakapan dapat diekspor ke format **Markdown (.md)** atau **Teks (.txt)** dengan 1 klik.

### 2. Pelacak Jatuh Tempo & Pengingat Otomatis (Contract & Deadline Tracker)
- Memindai secara otomatis seluruh dokumen untuk mendeteksi tanggal berlabel:
  - *“jatuh tempo”*, *“due date”*, *“masa berlaku hingga”*, *“tanggal kedaluwarsa”*, *“berakhir pada”*.
- Tab visual khusus dengan status urgensi:
  -  **Lewat Jatuh Tempo** (Tanggal sudah terlewat).
  -  **Kritis (<= 7 Hari)** (Perlu penanganan segera).
  -  **Segera (<= 30 Hari)** (Mendekati tenggat waktu).
  -  **Normal / Aman** (> 30 Hari).
- Terintegrasi dengan **Notifikasi Desktop Windows (*System Tray Balloon Notification*)** jika ada dokumen penting yang mendekati batas waktu.

### 3.  Rekap Invoice Otomatis ke Tabel Excel (Document-to-Table)
- Sering pusing menyalin angka dari tumpukan invoice atau kwitansi satu per satu ke Excel?
- Cukup klik tombol **" Rekap Semua Invoice ke Tabel"**:
  - AI mengekstrak data dari seluruh PDF, Excel, dan Email EML menjadi tabel terstruktur:
    `[No. Invoice | Tanggal | Nama Vendor/Klien | Uraian Transaksi | Nilai Tagihan (IDR) | Status Pembayaran | File Sumber]`
  - Kartu statistik menampilkan total akumulasi tagihan terbayar vs belum lunas.
  - Tombol **" Ekspor ke Excel (.csv)"** mengekspor tabel dengan encoding UTF-8 BOM yang langsung rapi dan kompatibel di Microsoft Excel.

### 4.  Bandingkan Dua Dokumen (Document Diff AI)
- Menerima revisi kontrak baru atau proposal kerja dan bingung mencari pasal mana yang diubah?
- Pilih dua dokumen (misal: `Kontrak_Kerja_2024.pdf` vs `Kontrak_Kerja_2025.pdf`):
  - AI menganalisis dan mendeteksi:
    -  **Perubahan Nilai Biaya**: Mendeteksi perubahan angka (misal: *Rp 100jt naik menjadi Rp 120jt*).
    -  **Perubahan Durasi Waktu**: Mendeteksi perubahan jangka waktu (misal: *12 bulan diperpanjang ke 24 bulan*).
    -  **Perubahan Klausul & Pasal**: Mendeteksi pasal yang dihapus, klausul baru yang ditambahkan, atau penyesuaian redaksi pasal.
  - Menyajikan kartu perubahan terstruktur berwarna dan ringkasan eksekutif komparatif.

### 5.  Pencarian Dokumen & Filter Lengkap
- Tab pencarian tabular dengan filter rentang tanggal, filter ekstensi file, dan pencarian instan.
- Pratinjau cuplikan teks (*instant text preview*) di samping tabel hasil.
- Opsi ekspor hasil pencarian ke file CSV.

### 6.  Pemantauan Otomatis Dokumen (*Live FileSystemWatcher*)
- Daftarkan folder kerja Anda (misal: folder *Documents*, *Downloads*, atau folder proyek klien). Folder lain di komputer Anda **tidak pernah disentuh**.
- File baru yang disimpan atau dokumen yang diedit otomatis terdeteksi dan diindeks ulang di latar belakang secara bertahap tanpa membuat komputer lambat atau membekukan antarmuka aplikasi.

### 7.  100% On-Device & Privat (Tanpa Cloud)
- Tidak ada telemetri konten atau pengiriman berkas ke server pihak ketiga.
- Indeks disimpan secara lokal di `%LocalAppData%\AsistenFile\asisten_file.db` (SQLite dengan mode Write-Ahead Logging & FTS5 BM25).

### 8.  Dual AI Engine (Zero-Setup & Ollama)
- **Mesin Ekstraktif Lokal Cepat (Bawaan)**: Berjalan langsung seketika tanpa perlu mendownload model AI bergiga-giga. Mengekstrak fakta angka, nomor dokumen, tanggal, dan kutipan secara akurat (0 MB overhead).
- **Integrasi Ollama Lokal**: Mendukung model LLM lokal seperti `qwen2.5:1.5b`, `llama3.2:3b`, `llama3.2:1b`, atau `phi3:mini` untuk penalaran generatif alami.
- **Wizard Setup Otomatis**: Jika Ollama belum terpasang, aplikasi akan menawarkan bantuan untuk mendownload dan memasang Ollama serta model yang diinginkan hanya dengan 1 klik!

---

##  Pipeline Arsitektur Teknis

```
[Folder Terpilih Pengguna]
         │
         ▼
[1] Crawler / FileSystemWatcher (Deteksi otomatis file baru & perubahan)
         │
         ▼
[2] Ekstraktor Teks Multi-Format (PDF, DOCX, XLSX, TXT, MD, EML) + OCR Windows Native
         │
         ▼
[3] Smart Text Chunker (Pemotongan kalimat ±300-500 kata + overlap 50 kata + metadata)
         │
         ▼
[4] Local Dense Semantic Vectorizer (Vektor 256-dim L2-Normalized)
         │
         ▼
[5] Database SQLite Lokal (Tabel Files, Chunks, Embeddings Blob, & FTS5 BM25)
         │
         ▼
Saat Pengguna Bertanya:
[6] Natural Language Query Parser (Ekstraksi intent, rentang tanggal, filter format)
         │
         ▼
[7] Hybrid Search Engine (Pencarian kombinasi FTS5 BM25 + Cosine Similarity Vektor + Boosts)
         │
         ▼
[8] Context Re-Ranking (Pemilihan potongan teks paling relevan)
         │
         ▼
[9] Local LLM / Extractive Synthesizer (Sintesis jawaban strictly-grounded)
         │
         ▼
[10] Tampilan Antarmuka Windows Forms (Jawaban + Kartu Sitasi Interaktif + Tombol Buka File)
```

---

##  Persyaratan Sistem

- **Sistem Operasi**: Windows 10 (versi 19041 atau lebih baru) / Windows 11 (64-bit).
- **Runtime / SDK**: .NET 8.0 SDK atau .NET 8.0 Desktop Runtime.
- **RAM**: Minimal 8 GB (disarankan 16 GB jika ingin menjalankan LLM Ollama 3B).
- **Penyimpanan**: Minimal 500 MB untuk aplikasi & basis data indeks.
- **Ollama (Opsional)**: Untuk fitur jawaban generatif berbasis LLM. Tanpa Ollama pun, aplikasi tetap bekerja 100% menggunakan Mesin Ekstraktif bawaan.

---

##  Panduan Instalasi & Menjalankan Aplikasi

---

### Metode 1: Langsung Klik File .EXE (Paling Cepat & Praktis) ⭐

Berkas **executable (.exe)** mandiri sudah selesai dibangun dan siap pakai di komputer Anda:
- **Lokasi File EXE**: [`Publish\AsistenFile.App.exe`](file:///c:/Users/asust/source/repos/Asisten%20File/Publish/AsistenFile.App.exe)
- **Shortcut Cepat**: Cukup klik 2x berkas [`Jalankan-AsistenFile.bat`](file:///c:/Users/asust/source/repos/Asisten%20File/Jalankan-AsistenFile.bat) di folder utama repositori.
- Berkas ini bersifat **Self-Contained Single-File**, artinya seluruh runtime .NET 8 dan komponen OCR Windows sudah dikemas di dalamnya sehingga dapat langsung dibuka tanpa perlu instalasi SDK tambahan.

---

### Metode 2: Menjalankan via Terminal / CLI

1. Buka terminal (PowerShell atau Command Prompt).
2. Masuk ke direktori repositori:
   ```powershell
   cd "c:\Users\asust\source\repos\Asisten File"
   ```
3. Jalankan aplikasi desktop Windows Forms:
   ```powershell
   dotnet run --project AsistenFile.App/AsistenFile.App.csproj
   ```

---

### Metode 3: Membuka di Visual Studio 2022

1. Buka file solusi `AsistenFile.slnx` di Visual Studio 2022 (versi 17.10+).
2. Di panel **Solution Explorer**, klik kanan pada proyek `AsistenFile.App` lalu pilih **Set as Startup Project**.
3. Pilih konfigurasi **Debug** atau **Release**, lalu tekan tombol **F5** (atau klik tombol Start) untuk menjalankan aplikasi.

---

### Cara Membangun Ulang File .EXE Mandiri (Jika Ada Perubahan Kode)

Jika Anda melakukan kustomisasi kode dan ingin memperbarui file `.exe` di folder `Publish`:

```powershell
dotnet publish AsistenFile.App/AsistenFile.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ".\Publish"
```

---

## 🤖 Panduan Penyiapan Model AI Ollama

Aplikasi ini memiliki fitur **Dual AI Engine**:
1. **Mode Bawaan (Default)**: Jika Ollama belum terpasang atau tidak aktif, aplikasi **langsung bekerja seketika** menggunakan `LocalExtractiveSynthesizer`. Tidak ada loading lama dan tidak ada instalasi paksa.
2. **Mode Ollama On-Device**: Memberikan kemampuan penalaran kontekstual yang lebih cerdas dan percakapan interaktif.

### Setup Otomatis di Dalam Aplikasi (In-App Wizard)
Saat pertama kali membuka aplikasi atau melalui tab ** Pengaturan AI**:
1. Klik tombol **" Buka Panduan Instalasi Ollama & Model"**.
2. Jika Ollama belum terdeteksi di komputer Anda, klik tombol **" Unduh & Pasang Ollama Otomatis"**. Aplikasi akan mengunduh `OllamaSetup.exe` resmi dan menjalankan proses instalasi.
3. Setelah Ollama aktif, pilih model yang direkomendasikan:
   - **`qwen2.5:1.5b` (Sangat Direkomendasikan)**: Ukuran kecil (~1 GB), sangat cepat, kemampuan pemahaman bahasa Indonesia sangat baik, ideal untuk laptop dengan RAM 8 GB.
   - **`llama3.2:3b`**: Model cerdas serbaguna untuk laptop dengan RAM 16 GB.
   - **`llama3.2:1b`**: Model ultra ringan untuk efisiensi CPU maksimal.
4. Klik tombol **"Unduh Model Pilihan"**. Anda dapat memantau bar persentase unduhan secara *real-time* atau memilih opsi *"Unduh di Latar Belakang"* agar Anda tetap bisa menggunakan komputer tanpa terganggu.

### Setup Manual via Terminal (Alternatif)
Jika Anda lebih suka menginstal Ollama secara manual lewat terminal:
```powershell
# 1. Unduh dan pasang dari situs resmi
# https://ollama.com/download/windows

# 2. Jalankan perintah untuk mengunduh model rekomendasi
ollama run qwen2.5:1.5b

# 3. Buka aplikasi Asisten File, status AI akan otomatis berubah menjadi "Ollama Aktif"
```

---

##  Panduan Penggunaan Aplikasi Langkah-demi-Langkah

### 1. Menambahkan Folder yang Diindeks (Langkah Awal)
1. Buka tab **" Kelola Folder"** pada menu sidebar kiri.
2. Klik tombol **" Tambah Folder"**.
3. Pilih folder yang berisi dokumen-dokumen penting Anda (misalnya folder `Documents`, `Downloads`, atau folder arsip kontrak/invoice).
4. Aplikasi akan langsung memindai seluruh dokumen, mengekstrak teks, dan menyimpannya ke indeks lokal. Anda dapat melihat progres pemindaian melalui **Progress Bar**.

### 2. Bertanya Dokumen pada Tab " Tanya & Chat"
1. Klik menu **" Tanya & Chat"**.
2. Anda bisa langsung mencoba mengklik salah satu **Chip Pertanyaan Cepat**, misalnya:
   - *“cari kontrak sewa yang saya tanda tangani tahun lalu”*
   - *“rangkum semua invoice bulan Maret”*
3. Atau ketik pertanyaan apa saja di kotak input bawah, lalu tekan tombol **Enter** atau tombol **" Cari Jawab"**.
4. Aplikasi akan menampilkan jawaban terstruktur beserta **Kartu Sumber Sitasi**:
   - Klik tombol **" Buka File"** untuk langsung membaca file asli.
   - Klik tombol **" Buka Folder"** untuk membuka lokasi folder penyimpanan file.
   - Klik tombol **" Salin Kutipan"** untuk menyalin cuplikan teks ke clipboard.

### 3. Menggunakan " Pelacak Jatuh Tempo"
1. Klik menu **" Jatuh Tempo"**.
2. Klik tombol **" Pindai Ulang Dokumen"**.
3. Aplikasi akan menampilkan seluruh dokumen yang memiliki tanggal jatuh tempo, lengkap dengan sisa hari dan status urgensinya.
4. Anda dapat memfilter tampilan berdasarkan: *Segera Jatuh Tempo (<= 30 hari)*, *Kritis (<= 7 hari)*, *Sudah Lewat Jatuh Tempo*, atau filter khusus kontrak/invoice.

### 4. Menggunakan " Rekap Invoice Otomatis"
1. Klik menu **" Rekap Invoice"**.
2. Klik tombol **" Rekap Semua Invoice ke Tabel"**.
3. Seluruh berkas invoice (PDF, Excel, Email) akan langsung diurai dan ditampilkan dalam tabel terstruktur.
4. Tinjau statistik total nilai tagihan, jumlah berkas, transaksi yang sudah lunas, dan yang belum dibayar.
5. Klik tombol **" Ekspor ke Excel (.csv)"** untuk menyimpan hasil rekapitulasi ke file Excel.

### 5. Menggunakan " Bandingkan File (Diff AI)"
1. Klik menu **" Bandingkan File"**.
2. Pada **Dokumen Lama (A)**, klik *"Pilih File A..."* lalu pilih versi lama dokumen Anda.
3. Pada **Dokumen Baru (B)**, klik *"Pilih File B..."* lalu pilih versi baru / revisinya.
4. Klik tombol **" Bandingkan Sekarang"**.
5. AI akan menampilkan perbandingan nilai biaya, durasi waktu pelaksanaan, dan klausul/pasal yang diubah atau dihapus.

---

##  Struktur Proyek

```
Asisten File/
├── AsistenFile.slnx                     # Solusi proyek utama (.NET 8)
├── README.md                            # Dokumentasi lengkap & panduan penggunaan
│
├── AsistenFile.Core/                    # Library Inti (Algoritma, RAG, Parser & Layanan)
│   ├── Models/
│   │   ├── DocumentModels.cs            # Model data: DocumentFile, DocumentChunk, SearchQuery, dll.
│   │   └── AdvancedFeatureModels.cs     # Model data: DeadlineItem, InvoiceItem, DocumentDiffResult
│   ├── Ocr/
│   │   └── WindowsMediaOcrService.cs    # Integrasi OCR Windows 10/11 native (tanpa Tesseract eksternal)
│   ├── Extractors/                      # Modul ekstraksi berkas multi-format
│   │   ├── ITextExtractor.cs            # Antarmuka ekstraktor
│   │   ├── ExtractorFactory.cs          # Factory pendaftaran ekstensi file
│   │   ├── PdfTextExtractor.cs          # Ekstraktor PDF (PdfPig + OCR scan otomatis)
│   │   ├── DocxTextExtractor.cs         # Ekstraktor Microsoft Word DOCX (OpenXML)
│   │   ├── ExcelTextExtractor.cs        # Ekstraktor Excel XLSX, XLS & CSV (ExcelDataReader)
│   │   ├── PlainTextExtractor.cs        # Ekstraktor Catatan TXT, Markdown MD, Log, JSON
│   │   ├── EmailTextExtractor.cs        # Ekstraktor Email EML & MSG (MimeKit)
│   │   └── ImageOcrExtractor.cs         # Ekstraktor Gambar PNG, JPG, BMP via OCR Windows
│   ├── Chunking/
│   │   └── SmartTextChunker.cs          # Pemotong teks berbasis batas kalimat (300-500 kata + overlap)
│   ├── Embeddings/
│   │   └── EmbeddingServices.cs         # Vektorisasi semantik lokal (256-dim) & Ollama fallback
│   ├── Storage/
│   │   └── SqliteDatabaseService.cs     # Database SQLite lokal (WAL mode, Chunks_FTS BM25)
│   ├── Search/
│   │   ├── NaturalLanguageQueryParser.cs# Parser bahasa alami (waktu, tanggal, ekstensi, intent)
│   │   └── HybridSearchEngine.cs        # Hybrid search (FTS BM25 + Cosine Vector + Boosts)
│   ├── Indexing/
│   │   ├── IndexingService.cs           # Layanan pemindaian file & background worker
│   │   └── FileWatcherService.cs        # Live FileSystemWatcher dengan debouncing 1,5 detik
│   ├── Features/                        # Layanan fitur lanjutan
│   │   ├── DeadlineTrackerService.cs    # Pemindai tanggal kedaluwarsa & batas waktu dokumen
│   │   ├── InvoiceExtractorService.cs   # Ekstraksi tabel transaksi invoice & ekspor CSV
│   │   └── DocumentDiffService.cs       # Analisis komparatif selisih dua versi dokumen
│   ├── AI/
│   │   ├── LanguageModelServices.cs     # Klien koneksi Ollama HTTP (127.0.0.1:11434)
│   │   ├── LocalExtractiveSynthesizer.cs# Mesin sintesis jawaban faktual 100% offline tanpa model
│   │   ├── RagOrchestrator.cs           # Orkestrator RAG terintegrasi dengan validasi groundedness
│   │   └── OllamaInstallerService.cs    # Layanan installer & background downloader model Ollama
│   └── Config/
│       └── SettingsService.cs           # Manajemen konfigurasi dan preferensi privasi pengguna
│
├── AsistenFile.App/                     # Aplikasi Antarmuka Windows Forms (.NET 8)
│   ├── Program.cs                       # Titik masuk aplikasi
│   ├── UI/
│   │   ├── ModernTheme.cs               # Palet warna modern, tipografi, dan gaya tombol/kartu
│   │   ├── MainForm.cs                  # Shell jendela utama (Sidebar, Header, Kontrol Tampilan)
│   │   ├── OllamaSetupDialog.cs         # Dialog wizard unduh Ollama & model AI
│   │   ├── Components/
│   │   │   ├── SourceCardControl.cs     # Kartu sitasi interaktif sumber dokumen
│   │   │   └── ChatBubbleControl.cs     # Balon percakapan tanya-jawab interaktif
│   │   └── Views/
│   │       ├── ChatView.cs              # Tampilan tanya-jawab bahasa alami & chip saran
│   │       ├── SearchGridView.cs        # Tampilan tabel pencarian berkas & filter tanggal
│   │       ├── DeadlineTrackerView.cs   # Tampilan pelacak jatuh tempo & agenda dokumen
│   │       ├── InvoiceRekapView.cs      # Tampilan rekapitulasi invoice & ekspor Excel
│   │       ├── DocumentDiffView.cs      # Tampilan pembanding dua versi dokumen
│   │       ├── FolderManagerView.cs     # Tampilan kelola folder & status indeksasi
│   │       └── SettingsView.cs          # Tampilan konfigurasi model AI, OCR, dan privasi
│
└── AsistenFile.Tests/                   # Rangkaian Pengujian Otomatis (xUnit)
    ├── NaturalLanguageQueryParserTests.cs
    ├── LocalSemanticEmbeddingTests.cs
    ├── ExtractorsTests.cs
    ├── HybridSearchAndRagIntegrationTests.cs
    ├── AdvancedFeaturesTests.cs
    └── RobustnessAndEdgeCasesTests.cs
```

---

##  Pengujian Otomatis & Verifikasi Kualitas

Proyek ini dilengkapi dengan **21 pengujian otomatis (*automated unit & integration tests*)** yang menguji seluruh fitur dari ujung ke ujung:

```powershell
dotnet test
```

### Hasil Uji:
- **Natural Language Query Parser**: Verifikasi deteksi waktu bahasa Indonesia (*“tahun lalu”*, *“bulan Maret”*), pemisahan kata kunci, dan filter format.
- **Local Semantic Embeddings**: Verifikasi normalisasi L2 vektor dimensi 256 dan konsistensi skor kosinus.
- **Multi-Format Extractors**: Verifikasi pembacaan Markdown, TXT, CSV/Excel, dan Email EML.
- **Hybrid Search & Grounded RAG**: Verifikasi integrasi SQLite FTS5 BM25, vektorisasi semantik, dan penolakan query yang tidak relevan.
- **Advanced Features**:
  - Pelacak tanggal kedaluwarsa & kalkulasi batas waktu (lewat waktu, kritis, aman).
  - Ekstraksi transaksi invoice ke tabel dan kalkulasi total akumulasi.
  - Pembanding dokumen (perubahan biaya, waktu pengerjaan, dan klausul).
- **Edge Cases & Robustness**: Pengujian dokumen kosong, dokumen teks sangat panjang, penyimpanan pengaturan, dan ketahanan terhadap file yang sedang dibuka di program lain (*file-sharing locks*).

---

##  Kebijakan Privasi & Keamanan Data

- **Tanpa Internet Wajib**: Aplikasi dapat berjalan normal dan menjawab pertanyaan dokumen dalam kondisi komputer *offline* sepenuhnya (tanpa koneksi internet).
- **Penyimpanan Lokal Eksklusif**: Basis data indeks dan embedding tersimpan aman di direktori lokal pengguna (`%LocalAppData%\AsistenFile\`).
- **Tanpa Pengumpulan Data / Telemetri**: Tidak ada analitik penggunaan, rekaman isi dokumen, atau pelacakan identitas apa pun yang dikirim keluar.
- **Kepatuhan Hak Akses Dokumen**: Aplikasi hanya membaca dan mengindeks folder yang secara eksplisit dipilih oleh pengguna.
