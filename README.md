# Revit Sekme Renkleri

Revit 2026 için bir eklenti: açık görünümlerin **sekmelerini belgeye göre renklendirir ve gruplar**. Aynı anda birden fazla proje ve aile açıkken hangi sekmenin hangi modele ait olduğunu bir bakışta görürsünüz.

*(Turkish-language edition. Interface, installer messages and this page are in Turkish; Revit 2026 only.)*

## Neler yapar?

- **Belge başına renk.** Açılan her proje ya da aile, 20 renkli paletten sıradaki rengi alır. Projeler ve aileler ayrı ayrı **dolgulu** ya da **kalın çerçeveli** gösterilebilir.
- **On görünüm.** Revit varsayılanı, Köşeli, Yuvarlak köşeli, Oval, Renk çubuklu, Alt çizgili, Yan şeritli, Bitişik bölümler, Yumuşak ve Paralelkenar. Stil listesinde her birinin yanında, sizin ayarlarınızla çizilmiş küçük bir örnek görürsünüz.
- **İki satırlı gruplama.** Üst satırda her belge için bir sekme, altında yalnızca etkin belgenin açık görünümleri. Bir belgeye tıkladığınızda o belgede en son kullandığınız görünüm açılır. İsterseniz üst satırdaki sekmeler klasör biçiminde görünür.
- **Üzerine gelince önizleme.** Sekmenin üzerinde seçtiğiniz süre kadar (0,1–5 sn) beklerseniz görünümün küçük bir resmi çıkar. Tablolarda (schedule) önizleme yoktur.
- **Boyutlar ve palet.** Dolgu yoğunluğu, çerçeve kalınlığı, köşe yuvarlaklığı ve ek sekme yüksekliği ayarlanabilir; paletteki her renk, renk seçici ile değiştirilebilir.
- **Açık ve koyu tema.** Ayar penceresi Revit'in temasını izler.

Tüm ayarlar tek pencerede yapılır ve **hemen uygulanır**. Eklentiyi tamamen kapatan bir ana anahtar da vardır.

## Kurulum

1. [Releases](../../releases) sayfasından `RevitSekmeRenkleri_Setup_<sürüm>.exe` dosyasını indirin.
2. **Revit'i kapatın** ve dosyayı çalıştırın. Kurulum yalnızca sizin kullanıcınız için yapılır, yönetici yetkisi gerekmez.
3. Revit'i açın: **Eklentiler** sekmesinde **Sekme Renkleri** grubunu ve **Sekme Renkleri** düğmesini bulursunuz. Düğme ayar penceresini açar.

Kurulum dosyası iki sürüm içerir: Revit 2026.4 ve öncesi için .NET 8, 2026.5 ve sonrası için .NET 10. Bilgisayarınızdaki Revit'e uygun olanı kendisi seçer.

> Kurulum dosyası dijital olarak imzalı değildir; ilk çalıştırmada Windows ya da antivirüs programınız onay isteyebilir.

**Kaldırmak için:** Revit kapalıyken `%APPDATA%\Autodesk\Revit\Addins\2026\RevitSekmeRenkleri` klasöründeki `setup-deinstallieren.cmd` dosyasını çalıştırın.

## Ayarlar neyi değiştirir?

| Ayar | Anlamı |
|---|---|
| **Sekme renkleri açık** | Ana anahtar. Kapalıyken Revit'in sekmeleri olduğu gibi kalır. |
| **Stil** | Sekmelerin biçimi (köşeli, yuvarlak, oval, paralelkenar …). |
| **İşaretleme – Projeler / Aileler** | Her belge türü için ayrı: *Dolgulu* ya da *Çerçeveli (dolgusuz)*. |
| **Görünüm** | Etkin ve pasif sekmenin dolgu yoğunluğu, çerçeve kalınlığı, köşe yuvarlaklığı, ek sekme yüksekliği. |
| **Gruplama** | Belgeye göre iki satırlı sekme çubuğu; isteğe bağlı klasör biçimi. |
| **Üzerine gelince önizleme** | Önizlemenin görünüp görünmeyeceği, gecikmesi, resim boyutu. *Yalnızca etkin belgenin görünümleri* seçeneği, önizleme hazırlanırken Revit'in başka belgeye geçmesini önler; büyük modellerde önerilir. |
| **Renk paleti** | 20 renk. Aileler paleti tersten okur, böylece aynı sıradaki proje ile aile farklı renk alır. Bir kutuya tıklayınca renk seçici açılır. |

## Bilgisayarınızdaki dosyalar

| Ne | Nerede |
|---|---|
| Ayarlar | `%LOCALAPPDATA%\RevitSekmeRenkleri\TabColor.json` |
| Kayıt dosyası (hata ararken işe yarar) | `%LOCALAPPDATA%\RevitSekmeRenkleri\TabColor-Log.txt` |
| Kurulum kaydı | `%LOCALAPPDATA%\RevitSekmeRenkleri\Setup\RevitSekmeRenkleri-setup.log` |

## Geliştiriciler için

.NET SDK ve Revit API başvuru dosyaları gerekir. `src/Revit.props` iki Revit sürümünü tek kaynak koddan derler: kurulu Revit 2026 kendi sürümünü sağlar; diğer sürüm için ilgili güncellemeye sahip bir bilgisayardan `RevitAPI.dll` ve `RevitAPIUI.dll` dosyalarını `C:\RevitRefs\2026.4` (.NET 8) ya da `C:\RevitRefs\2026.5` (.NET 10) klasörüne kopyalayın.

```powershell
# bu bilgisayara derle ve kur (Revit kapalı olmalı)
powershell -ExecutionPolicy Bypass -File build\deploy.ps1
powershell -ExecutionPolicy Bypass -File build\deploy.ps1 -Entfernen   # tekrar kaldır

# iki sürümü de derle ve kurulum dosyasını dist\ klasörüne paketle
powershell -ExecutionPolicy Bypass -File build\pack.ps1
```

Kod yorumları ve kayıt dosyası mesajları Almancadır; kullanıcıya görünen tüm metinler Türkçedir.

### Bilinmesi gerekenler

- Eklenti Revit'in belgelenmemiş arayüz bölümlerini (`UIFramework`, `Xceed.Wpf.AvalonDock`) ve arayüz iş parçacığında 500 ms'lik bir zamanlayıcı kullanır; çünkü Revit her görünüm değişiminde sekme stilini sıfırlar. Bir tur yaklaşık 1–2 ms sürer.
- Bir sekme, ait olduğu belgeye **sırasına göre değil**, kendi `LayoutDocumentTabItem.Model` değeri üzerinden eşlenir; sekmelerin sırası AvalonDock modeliyle aynı değildir.
- Diğer belgelerin sekmeleri `Visibility` ile değil, genişliği 0 yaparak gizlenir; sekme paneli `Visibility` değerini kendisi sıfırlar.
- Önizleme için kullanılan `Document.ExportImage`, Revit'in o görünümün belgesine geçmesine yol açar; eklenti sonrasında geri döner.
- Sekme çubuğunun sağ ucundaki oku (tüm açık görünümlerin listesi) **değiştirmeyin**: öğelerini değiştirmek Revit'i iki kez çökertti.
- *Paralelkenar* stili ve klasör biçimli belge sekmesi, Revit'in dikdörtgen sekme şablonunun içine yerleştirilmiş çizimlerdir.
- Yeni, henüz kaydedilmemiş aileler Revit'te `Aile1` gibi adlarla görünür; eklenti bunları dosya uzantısı olmadığı için aile olarak tanır.

Yalnızca Revit 2026 hedeflenmiştir.
