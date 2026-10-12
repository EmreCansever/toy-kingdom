# Toy Kingdom

Oyuncak tarzında, 2D izometrik bir mobil strateji oyunu. Krallığın en köşedeki başkentinden yola çıkar,
haritadaki şehirleri tek tek fethedersin. Her şehirde farklı savunmaları olan bir kale vardır; saldırıda
bölüklerini parmağınla çizdiğin rotalardan gönderirsin ("ters kule savunması").

Unity 6 (6000.3) ile geliştiriliyor. Hedef platform: yatay ekranlı telefonlar.

## Özellikler

- **Dünya haritası:** Kaydırılıp yakınlaştırılabilen izometrik ada; yollar, nehir, köprüler, ormanlar, köyler.
  Şehre dokununca şövalye yollar boyunca oraya yürür. Fethedilen şehirlerin kuleleri maviye döner ve
  dakikada altın üretir (oyun kapalıyken de birikir).
- **Ordu:** Altınla bölük alınır: Piyade, Kalkanlı, Okçu, Merdivenci, Ağır şövalye.
- **Savaş:** Bölük kartı seç, sol taraftaki işaretli bölgeden kaleye doğru rota çiz. Bölük düzen içinde yürür,
  savunucularla çarpışır, rota bitince kapıya saldırır. Okçular uzaktan vurur, merdivenciler kulelere tırmanıp
  onları yıkar. Bir kapı kırılınca kale düşer.
- **Savunma:** Bloklardan kurulan kaleler, ok atan ve yıkılabilen kuleler, sur üstü okçular, kale önüne dağılıp
  devriye gezen savunucular ve savaş sırasında kapıdan çıkan takviyeler. Kaleler zorlaştıkça büyür.

- **Krallık yönetimi:** Fethedilen şehirlere vali ata (sadakat, güç, özellik), vergi ayarla, Pazar/Kışla/Sur/Tapınak kur.
  Huzursuzluk ya da sadakatsiz vali isyana yol açar; asi şehir savaşla geri alınır.
- **Kuşatma derinliği:** Moral ve bozgun, kapıya koşan savunucular, kızgın yağ, okçulara akın, kapı kırılınca son direniş
  (kapının önünü tut), ordu kapasitesi. Geniş meydan: batı, kuzey ve güney kenarlarından konuşlanma, kaydırılabilir kamera.
- **Rakip krallıklar:** Kum Sultanlığı (çöl) ve Buz Krallığı (kış) güç toplar, tarafsız şehirleri alır, zamanla senin
  şehirlerine önceden haber verilen saldırılar düzenler. Sur, kışla, vali ve takviye bölükleriyle savunursun; rakip
  başkenti düşerse krallık dağılır ve kalan şehirleri sana geçer.

- **Saray ve diplomasi:** Her krallığın bir hükümdarı var (Sultan Kara Davud: açgözlü ve sinsi; Kraliçe Ayaz Hatun:
  gururlu ve onurlu). Krallar mektup yollar: saldırmadan önce haraç ister, savaş ilan eder, barış önerir, aldıkları
  şehri satmayı ya da esirleri fidyeyle vermeyi teklif eder. Her mektubun cevap süresi vardır; cevap vermezsen en kötü
  seçenek uygulanır. Diplomasi panelinden hediye gönder, barış öner, haraç iste ya da savaş ilan et.
- **Hazine ve şehir kontrolü:** Bölükler ve valiler maaş alır; hazine borca girerse halk huzursuzlanır, askerler kaçar.
  Şehirlere kararname (Ticaret / Askeri / Huzur) çıkar, halktan asker topla. Vezir kıtlık, salgın, yolsuzluk, kervan
  ve şenlik gibi kararlar ister.
- **İtibar:** Hükümdarın itibarı zaferler, adil yönetim ve sözünde durmakla yükselir; isyanı son anda parayla
  bastırmak, haraç ödemek, yemin bozmak ve şehir kaybetmek düşürür. Halkın huzurunu, valilerin sadakatini, aday
  kalitesini, rakip kralların tavrını ve isyanların gücünü etkiler.
- **Bildirimler ve günlük:** Kritik haberler kırmızı ve öne geçer, önemsizler altta kısa bir şeritte akar; her şey
  Günlük panelinde saklanır.
- **Ticaret ve yol güvenliği:** Kendi iki şehrini bağlayan her yolda kervanlar gidip gelir ve altın taşır. Yolun
  güvenliği düşerse eşkıyalar kervanları soyar, köyleri yağmalar, şehirler huzursuzlanır ve çıkan isyanlara katılır.
  Ordudan ayırdığın bölüklerle yollara devriye ata (atlı şövalye en iyisi); Sur ve Askeri kararname de güvenliği artırır.

## Başlarken

1. Repoyu klonla ve klasörü **Unity Hub > Add > Add project from disk** ile ekle (Unity 6000.3.x).
2. Proje açılınca üst menüden **ToyKingdom > Örnek içeriği oluştur**'u çalıştır.
   Bu, birlikleri, kaleleri ve `Map` / `Battle` sahnelerini `Assets/ToyKingdom/Generated` altına yeniden kurar.
   (TextMeshPro kaynakları istenirse **Window > TextMeshPro > Import TMP Essential Resources**.)
3. `Map` sahnesinde **Play**. Game/Simulator görünümünü yatay kullan.

Kayıt sıfırlamak için: **ToyKingdom > Kaydı sıfırla**.

## Proje yapısı

```
Assets/ToyKingdom/
  Art/Medieval        İzometrik ortam ve kale grafikleri
  Art/Crusader        8 yönlü şövalye animasyonları
  Data/WorldLayout.json   Dünya haritası yerleşimi
  Scripts/Core        İzometrik ızgara, derinlik sıralaması, dokunma, kamera, animasyon, UI, kayıt
  Scripts/Data        UnitData, CastleData, TowerData, CharacterAnimSet
  Scripts/Battle      BattleManager, BattleField, Squad, Soldier, Tower, PathDrawer, arayüz
  Scripts/Map         MapController, MapCity, MapHero, arayüz
  Scripts/Editor      İçerik oluşturucu ve grafik içe aktarma kuralları
  Generated/          Oluşturucunun ürettiği veri ve sahneler
Tools/
  worldgen.py         Harita yerleşimini üretir (WorldLayout.json)
  preview.py          Haritanın önizleme resmini çizer
```

Yeni kale: **Create > ToyKingdom > Kale** ile bir `CastleData` oluştur. Savaş alanı bu veriden çalışma anında kurulur.
Yeni şehir: `Tools/worldgen.py` içindeki `CITIES` / `LINKS` listelerini düzenle, betiği çalıştır, içeriği yeniden oluştur.

## Grafikler ve lisanslar

- **Isometric Medieval Pack** — Artyom Zagorskiy — [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)
  ([itch.io](https://artyom-zagorskiy.itch.io/isometric-medieval-pack))
- **Isometric Mini Crusader** — Bleed - http://remusprites.carbonmade.com/ — [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)
  ([OpenGameArt](https://opengameart.org/content/isometric-mini-crusader))

Her paketin lisans metni kendi klasöründe (`License.txt`) bulunur. Mini Crusader lisansı gereği yukarıdaki isim
oyunun "Emeği geçenler" ekranında da yer almalıdır.

### Ses ve müzik (hepsi CC0)

- **Müzik:** RandomMind — Medieval: The Old Tower Inn, Medieval: Battle, Medieval Victory Theme, Medieval: King's Feast (OpenGameArt)
- **Kenney** — RPG Audio, Interface Sounds (kenney.nl)
- **StarNinjas** — 20 Sword Sound Effects (OpenGameArt)
- **Thimras** — Cannon Fire (OpenGameArt)
- **artisticdude** — Battle Sound Effects (OpenGameArt, CC0 seçeneği)
