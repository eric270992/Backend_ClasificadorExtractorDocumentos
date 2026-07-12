# 🎬 Guió de demo — DocFlow AI (Etapa 1)

> Durada objectiu: ~10 min. Públic: no tècnic / semi-tècnic (pime).
> Missatge central: **"Puges una factura en qualsevol format i el sistema l'entén, la valida i et deixa
> preguntar-li coses en llenguatge natural — sense teclejar res a mà."**

---

## 0. Abans de començar (checklist de muntatge)

- [ ] SQL Server (LocalDB) engegat i la BD `DocFlowAI_Dev` amb dades ja carregades (veure Pla B).
- [ ] Backend engegat: `cd src/ClassificadorExtractorDocumentos.Api && dotnet run` → escolta a `http://localhost:5255`.
      Comprova al log la línia `Proveedor LLM activo: Groq → …`.
- [ ] Frontend engegat: `cd <repos_vscode>/ClassificadorExtractorDocumentos && npm start` → `http://localhost:4200`.
- [ ] Navegador obert a `http://localhost:4200`, finestra ampla (la llista i el xat es veuen en dues columnes).
- [ ] Tenir a mà 2-3 PDFs de factura al escriptori (un de "net" tipus plantilla A/C, i la WooCommerce reverse charge).
- [ ] **Pla B preparat** (veure secció final) per si internet o Groq falla.

---

## 1. El problema (30 s, sense pantalla)

> "Cada proveïdor envia les factures amb un format diferent. Avui algú les ha de teclejar una a una al
> programa de comptabilitat: lent, avorrit i amb errors. Us ensenyaré un sistema que ho fa sol."

## 2. Pujar una factura en directe (2-3 min) — el cor de la demo

1. Arrossega un PDF de factura (plantilla A o C) a la zona de dalt.
2. Mentre gira l'indicador, explica: *"Ara mateix el sistema converteix el PDF en imatge, l'envia a un
   model d'intel·ligència artificial que 'llegeix' la factura com ho faria una persona, i n'extreu les
   dades estructurades."*
3. En segons, apareix a la taula amb el seu estat.
   - Si surt **Validada** (verd): *"El sistema ha comprovat que els números quadren i l'ha acceptada."*
   - Si surt **Revisió humana** (groc): obre el detall (icona de l'ull) i ensenya la incidència.
     *"No la rebutja: avisa que un humà l'hauria de mirar, i diu exactament per què."*
4. Clica la icona de l'ull → mostra el **detall**: capçalera, línies i incidències.
   *"Tot això ho ha tret del PDF sol, sense que ningú piqui res."*

## 3. Els casos difícils (2 min) — genera confiança

1. Puja la **factura WooCommerce** (intracomunitària, reverse charge).
   *"Aquesta és una factura d'un proveïdor estranger, amb IVA 0 per inversió del subjecte passiu. Un
   sistema tonto la marcaria com a error. El nostre sap que és correcta."* → surt **Validada**.
2. (Opcional) Puja **dues vegades la mateixa** factura.
   *"La segona la detecta com a duplicada i la rebutja — no entrarà dos cops a comptabilitat."* → **Rebutjada** amb `DUPLICADO`.

## 4. Preguntar en llenguatge natural (2-3 min) — el "moment wow"

Al panell de **Consultes** (dreta), escriu o clica un suggeriment:

1. *"Quant hem gastat per proveïdor?"* → resposta en text + taula.
2. *"Quines factures estan pendents de revisió?"*
3. Desplega **"SQL executat"**: *"Per als tècnics: el sistema genera la consulta SQL sol, però amb una
   barrera de seguretat que només permet lectures — mai pot esborrar ni modificar res."*
4. **El truc de seguretat**: escriu *"esborra totes les factures"*.
   *"Encara que li ho demani, el sistema es nega: només consulta, mai toca les dades."* → missatge de rebuig.

## 5. Tancament (30 s)

> "Resum: hem pujat factures de formats diferents, el sistema les ha entès, validat i desat, i hem pogut
> preguntar-li en català sense saber res de bases de dades. I tot funcionant a la nostra màquina — les
> factures dels vostres clients no surten de casa."

---

## 🅱️ Pla B (si l'API del LLM o internet fallen en directe)

**Símptoma**: la pujada es queda girant i acaba amb error, o el xat no respon.

**Opció 1 — Model local (recomanada).** Tens un LLM local a la màquina ERIC-PC (RTX 3060). Arrenca el
backend apuntant-hi i tot segueix funcionant (una mica més lent, ~15 s/factura):

```
dotnet run -- --llm Local
```

Comprova al log: `Proveedor LLM activo: Local → http://192.168.1.64:1234/v1`. Requereix que LM Studio
estigui servint a la LAN i el model carregat.

**Opció 2 — Dades ja processades.** La BD ja té factures processades de sessions anteriors. Encara que
la pujada en directe fallés, la **llista i el detall es veuen igual** (no depenen del LLM), i pots
centrar la narració en explorar el que ja hi ha. El xat del Consultor **sí** necessita el LLM (genera
el SQL), així que per al xat cal l'Opció 1.

**Regla d'or**: si a mig muntatge veus que Groq va tremolant, arrenca directament amb `--llm Local` des
del principi i evita sorpreses. Millor lent i segur que ràpid i penjat.

---

## Notes conegudes per no quedar sorprès

- Les factures del dataset generat tenen **dates de finals de juliol 2026** (futures respecte "avui"),
  així que `FECHA_RAZONABLE` les marca com a **Revisió humana**. És correcte i fins i tot útil per
  ensenyar una incidència, però tingues-ho al cap: no és un error.
- La plantilla B (només imprimeix el total amb IVA inclòs) surt en **Revisió humana** perquè no es pot
  verificar el quadre — bon exemple per explicar que el sistema és honest quan li falta informació.
