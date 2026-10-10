# CV REST API

Semestrální projekt v C# / ASP.NET Core 8. SQLite přes Entity Framework Core, JWT s expirací, Swagger UI, extrakce a kontrola dokumentů bez AI, strukturovaný výstup OpenAI a CRUD. K běhu není potřeba Microsoft Word ani databázový server.

## Spuštění

Vyžaduje .NET SDK 8 nebo novější se schopností sestavit `net8.0` a ASP.NET Core Runtime 8. Příkazy spouštějte z kořene řešení.

```powershell
dotnet restore
./scripts/Start-Local.ps1
```

Skript se na nic neptá. Vygeneruje náhodný JWT klíč do prostředí procesu a při prvním spuštění vytvoří lokální testovací účet `admin` s heslem `LocalDemo123!`. Nastavené proměnné `SeedUser__Username` a `SeedUser__Password` mají přednost. Existující účet se při opakovaném spuštění nepřepisuje a zachovává původní heslo. Výchozí účet je určen pouze pro lokální vývoj. Swagger: [http://localhost:5080/swagger](http://localhost:5080/swagger).

OpenAI klíč je volitelný a načítá se z `OpenAI__ApiKey` nebo lokální konfigurace; skript jej nevyžaduje. Přepínač `-WithOpenAI` je zachován pro kompatibilitu, ale nic dalšího nedělá. Žádný skutečný klíč není součástí projektu. Bez API klíče funguje přihlášení, extrakce, kontrola i CRUD; AI endpointy vracejí 503.

Pro trvalé lokální uložení klíče otevřete `src/CvApi/appsettings.Local.json` a doplňte hodnotu `OpenAI:ApiKey`:

```json
{
  "OpenAI": {
    "ApiKey": "SEM_VLOZTE_SVUJ_NOVY_KLIC"
  }
}
```

Soubor je v `.gitignore` a nepatří do odevzdávaného repozitáře. Po úpravě restartujte server. Pokud máte nastavenou proměnnou `OpenAI__ApiKey`, má přednost před souborem; pro použití konfigurace ji v PowerShellu odstraňte pomocí `Remove-Item Env:OpenAI__ApiKey -ErrorAction SilentlyContinue`.

Alternativně nastavte konfiguraci přes prostředí a spusťte:

```powershell
dotnet run --project src/CvApi --no-launch-profile --urls http://localhost:5080
```

| Proměnná | Význam |
| --- | --- |
| `Jwt__Key` | Povinný náhodný podpisový klíč, alespoň 32 UTF-8 bajtů |
| `Jwt__Issuer` / `Jwt__Audience` | Výchozí `CvApi` / `CvApiClients` |
| `Jwt__ExpiresMinutes` | Výchozí 30, omezeno na 1–120 minut |
| `SeedUser__Username` | Jméno účtu vytvořeného při prvním spuštění |
| `SeedUser__Password` | Heslo nového účtu, alespoň 12 znaků |
| `OpenAI__ApiKey` | Nový soukromý OpenAI API klíč |
| `OpenAI__Model` | Výchozí `gpt-4.1-mini`; musí podporovat Structured Outputs |
| `ConnectionStrings__CvDatabase` | Výchozí `Data Source=cv.db` |

Hesla se ukládají pouze jako hash přes ASP.NET Core PasswordHasher. SQLite soubor vzniká v pracovním adresáři aplikace. Přihlašovací údaje a API klíč lze také nastavit v ignorovaném `src/CvApi/appsettings.Local.json`; prostředí má přednost. `.env` není automaticky načítán. Při změně JWT klíče přestanou platit staré tokeny. Při nasazení používejte HTTPS; vývojový skript naslouchá pouze na localhost.

## Postup ve Swaggeru

1. `POST /api/auth/login` s `{"username":"admin","password":"LocalDemo123!"}` (u dříve vytvořeného účtu použijte původní heslo).
2. Zkopírujte `accessToken`, klikněte **Authorize** a vložte jej bez prefixu `Bearer`.
3. `POST /api/cv/check`: nahrajte původní soubor do pole `file` a prohlédněte si nálezy.
4. `POST /api/cv/extract`: získáte `text` (včetně podezřelých částí, pro fulltext), `safeText` (bez nalezených podezřelých částí) a `check`.
5. `POST /api/cv/parse`: předávejte **safeText** v těle `{"text":"..."}`. Požadavek musí obsahovat 1–100 000 znaků; samotné mezery se odmítají.
6. Výstup z parse vložte jako tělo do `POST /api/cvs`. Odpověď 201 obsahuje ID a hlavičku Location.
7. Seznam, detail, úprava pomocí PUT a odstranění pomocí DELETE.
8. `POST /api/cvs/{id}/analysis` s `{"jobDescription":"Hledáme vývojáře Java, Spring Boot a Docker."}` (max. 30 000 znaků).

Swagger dokumentace je veřejná, aby bylo možné provést přihlášení. Všechny aplikační endpointy kromě login vyžadují platný JWT; každý uživatel vidí a upravuje pouze svoje CV. Přístup k cizímu ID vrací 404.

## Endpointy a stavové kódy

| Metoda | Cesta | Úspěch | Specifické chyby |
| --- | --- | --- | --- |
| POST | `/api/auth/login` | 200 token + expirace | 400, 401, 429 |
| POST | `/api/cv/extract` | 200 text + safeText + check | 400, 413, 415, 422 |
| POST | `/api/cv/check` | 200 suspicious + issues | 400, 413, 415, 422 |
| POST | `/api/cv/parse` | 200 strukturované CV | 400, 422, 502, 503, 504 |
| POST | `/api/cvs` | 201 CV + Location | 400 |
| GET | `/api/cvs?page=1&pageSize=20` | 200 seznam | 400 |
| GET | `/api/cvs/{id}` | 200 detail | 404 |
| PUT | `/api/cvs/{id}` | 200 kompletně nahrazené CV | 400, 404 |
| DELETE | `/api/cvs/{id}` | 204 bez těla | 404 |
| POST | `/api/cvs/{id}/analysis` | 200 analýza | 400, 404, 422, 502, 503, 504 |

Zabezpečené endpointy mohou navíc vracet 401; neočekávané chyby vracejí 500 bez interních detailů. Chyby používají ProblemDetails, validace ValidationProblemDetails. Seznam je stránkovaný, nejvýše 100 položek. Login má limit 10 pokusů za minutu na IP adresu. PUT nahrazuje celý objekt, nejde o částečný PATCH.

Ukázka těla pro POST/PUT (stejná struktura jako výstup parse):

```json
{
  "firstName": "Jan",
  "lastName": "Novák",
  "email": "jan.novak@example.cz",
  "phone": "+420777123456",
  "address": null,
  "city": "Ostrava",
  "region": null,
  "postalCode": null,
  "nationality": null,
  "gender": null,
  "languages": [{ "language": "English", "level": "B2" }],
  "drivingLicenses": ["B"],
  "workExperience": [{ "position": "Java Developer", "company": "ABC", "from": "2023-01", "to": null }],
  "education": [{ "school": "VŠB-TUO", "field": "Informatika", "degree": "Ing." }]
}
```

Chybějící údaje jsou null nebo prázdné kolekce. Jazykové úrovně: A1–C2 nebo null. Data pracovních pozic: `YYYY` nebo `YYYY-MM`, probíhající zaměstnání má `to: null`. Nejvýše dvě pracovní pozice. Uložené CV se vrací v obálce `{id, createdAt, updatedAt, data}`; časová razítka jsou v UTC.

## Kontrola původních dokumentů

| Formát | Implementace | Detekce |
| --- | --- | --- |
| PDF | PdfPig, původní textové operace a souřadnice | neviditelný rendering 3/7, velikost pod 3 pt, téměř bílá barva, text mimo CropBox |
| DOCX | ZIP + bezpečné XML čtení | vanish/webHidden, pod 3 pt, FFFFFF; výchozí, odvozené, odstavcové i znakové styly a přímé formátování |

DOCX prochází také tabulky, textové boxy, záhlaví, zápatí a poznámky. Kontrola vrací typ, nalezený text, umístění a u drobného textu velikost v bodech. Heuristika rovněž označuje některé české a anglické pokusy o prompt injection.

Limity: 10 MB upload, 100 stran PDF, 500 000 znaků extrakce, 50 MB rozbaleného DOCX a 2 000 položek archivu. Typ se ověřuje podle přípony i signatury obsahu. Poškozené, šifrované nebo nepodporované varianty vracejí 422; nepodporované typy 415. Prázdný soubor vrací 400. Dokument bez textové vrstvy vrací 422; OCR se nepoužívá.

Kontrola je heuristická, nikoli úplný renderovací engine. Bílý text je podezřelý i na tmavém pozadí; pozadí se nerenderuje. DOCX nevyhodnocuje geometrii stránky. Překrytí objektů, průhlednost, komplexní clipping, všechny motivy Wordu a všechny varianty fontů nejsou pokryté. Starý formát DOC není podporován a vrací HTTP 415. Bezpečný výsledek neznamená důkaz absence skrytého obsahu.

## OpenAI a nedůvěryhodný obsah

Texty CV a pracovní nabídky jsou odděleny od systémových instrukcí a předány jako uživatelská data. Rozpoznané pokyny se odstraňují; model nemá nástroje ani oprávnění měnit aplikaci. Používá se [OpenAI Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs) s `strict: true`, všechny položky schématu jsou povinné a neznámá pole zakázaná. Odpověď se znovu kontroluje proti schématu a validačním pravidlům. Zpracovává se také odmítnutí modelu a neúplná odpověď. HTTP timeout je 60 sekund. Požadavky mají `store: false`.

Analýza předává pouze praxi, vzdělání, jazyky a řidičská oprávnění. Jméno, kontakty, adresa, pohlaví a národnost se do analýzy neposílají. Model dostává pokyn ignorovat osobní charakteristiky i diskriminační podmínky inzerátu a popsat pouze profesní shodu; nerozhoduje o přijetí a nepřiděluje pořadí.

Libovolnou prompt injection nelze garantovaně odstranit regexem ani promptem. API pro parse přijímá čistý text a nemůže zpětně určit jeho původní formátování. Proto používejte `safeText` z extrakce a před uložením ověřte výsledek; ručně vložené JSON CV rovněž nemá důkaz původu. AI výstup je podklad k lidskému posouzení, může obsahovat chyby.

## Databáze a struktura

`Users` obsahuje hash hesla. `Cvs` obsahuje vlastníka, časy a jednoduché údaje CV; jazyky, pracovní zkušenosti a vzdělání jsou samostatné relační tabulky s cizími klíči a kaskádovým mazáním. Řidičská oprávnění jsou primitivní JSON kolekcí uvnitř SQLite sloupce. Zápis CV i jeho kolekcí je transakční přes EF Core.

Schéma se při prvním spuštění vytvoří pomocí `EnsureCreated`. Projekt nemá historii migrací; před změnami schématu existující databáze je nutné doplnit EF migrace nebo použít novou testovací databázi. Data nemažte bez zálohy.

| Soubor | Účel |
| --- | --- |
| `src/CvApi/Program.cs` | konfigurace, JWT, databáze, middleware, Swagger |
| `src/CvApi/Controllers/` | REST rozhraní, každý controller ve vlastním souboru |
| `src/CvApi/Models/` | DTO a entity, každý typ ve vlastním souboru |
| `src/CvApi/Models/CvDb.cs` | EF databázový kontext a mapování |
| `src/CvApi/Services/InputSafety.cs` | validace AI výstupů a filtrace známých pokynů |
| `src/CvApi/Models/ApiException.cs` | aplikační výjimka s HTTP stavem |
| `src/CvApi/Services/DocumentService.cs` | extrakce a kontrola původních dokumentů |
| `src/CvApi/Services/OpenAiService.cs` | OpenAI požadavky, schémata a kontrola odpovědí |

## Sestavení

```powershell
dotnet build CvApi.sln --configuration Release
```

Endpointy lze ručně vyzkoušet ve Swagger UI podle postupu výše.

Audit přímých i nepřímých závislostí je zapnutý v `Directory.Build.props`. DOCX se zpracovává nativními knihovnami .NET (System.IO.Compression a System.Xml.Linq), bez NPOI a bez instalace Wordu. SQLite native bundle je explicitně aktualizovaný.
