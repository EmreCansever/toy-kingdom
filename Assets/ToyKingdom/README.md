# Toy Kingdom — sürüm 2 (izometrik)

## Kurulum
1. Unity'de **ToyKingdom > Örnek içeriği oluştur** menüsünü çalıştır.
2. Açılan `Map` sahnesinde **Play**. Simülatörü yatay (Rotate) kullan.

## Oynanış
- **Harita:** Parmakla sürükle (kaydır), iki parmakla veya fare tekerleğiyle yakınlaştır.
  Bir şehre dokun: şövalye yol boyunca oraya yürür. Fethedilmiş bir şehre yolla bağlı düşman şehrine saldırabilirsin.
- **Ordu:** Sağ üstteki düğmeyle altın karşılığı bölük al:
  Piyade (kalabalık), Kalkanlı (oklara dayanıklı), Okçu (uzaktan asker/kule/kapı vurur),
  Merdivenci (kulelere merdiven dayayıp tırmanır ve yıkar), Ağır şövalye (kapı kırıcı).
- **Savaş:** Alttan bir bölük kartı seç, işaretli (açık renkli) bölgeye parmağını bas ve kaleye doğru **rota çiz**.
  Bırakınca bölük düzen içinde yürür, yoldaki savunucularla çarpışır, rota bitince en yakın kapıya saldırır.
  Sadece dokunursan bölük doğrudan en yakın kapıya gider. Kulelerden ok yağar; kalkanlılar oklara dayanıklıdır,
  ağır şövalyeler kapıyı hızlı kırar. Bir kapı kırılınca kale düşer.
- **Savunma:** Savunucular kale önüne dağınık nöbet noktalarına yerleşir ve devriye gezer; yaklaşanı karşılar.
  Surların üstünde düşman okçuları durur (sadece okçuların erişebilir). Kuleler yıkılabilir.

## Yapı
```
Art/Medieval   Artyom Zagorskiy - Isometric Medieval Pack (CC0)
Art/Crusader   Bleed - Isometric Mini Crusader (CC-BY 4.0)  -> oyunda isim verilmeli (aşağıya bak)
Data/WorldLayout.json   Dünya haritası yerleşimi (Tools/worldgen.py üretir)
Scripts/Core    Iso ızgara, derinlik sıralaması, dokunma/sürükleme, kamera, animasyon, UI fabrikası, kayıt
Scripts/Data    UnitData, CastleData, TowerData, CharacterAnimSet
Scripts/Battle  BattleManager, BattleField (kale çalışma anında kurulur), Squad, Soldier, Tower, PathDrawer
Scripts/Map     MapController, MapCity, MapHero, arayüz
Scripts/Editor  SampleContentBuilder, ArtImportRules
```

## Yeni kale / şehir eklemek
- **Create > ToyKingdom > Kale** ile yeni `CastleData` oluştur: genişlik/derinlik, renk, ek kuleler, ikinci kapı,
  kapı canı, savunma bölükleri, takviye. Savaş alanı bu verilerden otomatik kurulur.
- Haritaya şehir eklemek için `Tools/worldgen.py` içindeki `CITIES` ve `LINKS` listelerini düzenle,
  `python worldgen.py ../Assets/ToyKingdom/Data/WorldLayout.json` çalıştır, sonra menüden içeriği yeniden oluştur.
  (`Tools/preview.py` haritanın önizleme resmini çizer.)

## Lisanslar ve emeği geçenler
- Isometric Medieval Pack — Artyom Zagorskiy — CC0 (isim vermek zorunlu değil)
- Isometric Mini Crusader — **Bleed - http://remusprites.carbonmade.com/** — CC-BY 4.0.
  Bu ismin oyunun "Emeği geçenler" ekranında ya da mağaza açıklamasında yer alması **zorunludur**.
