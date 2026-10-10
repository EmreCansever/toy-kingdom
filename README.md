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
