# 🎬 Guion de demo — DocFlow AI (Etapa 1)

> Duración objetivo: ~10 min. Público: no técnico / semi-técnico (pyme).
> Mensaje central: **"Subes una factura en cualquier formato y el sistema la entiende, la valida y te deja
> preguntarle cosas en lenguaje natural — sin teclear nada a mano."**

---

## 0. Antes de empezar (checklist de montaje)

- [ ] SQL Server (LocalDB) arrancado y la BD `DocFlowAI_Dev` con datos ya cargados (ver Plan B).
- [ ] Backend arrancado: `cd src/ClassificadorExtractorDocumentos.Api && dotnet run` → escucha en `http://localhost:5255`.
      Comprueba en el log la línea `Proveedor LLM activo: Groq → …`.
- [ ] Frontend arrancado: `cd <repos_vscode>/ClassificadorExtractorDocumentos && npm start` → `http://localhost:4200`.
- [ ] Navegador abierto en `http://localhost:4200`, ventana ancha (la lista y el chat se ven en dos columnas).
- [ ] Tener a mano 2-3 PDFs de factura en el escritorio (uno "limpio" tipo plantilla A/C, y la WooCommerce reverse charge).
- [ ] **Plan B preparado** (ver sección final) por si internet o Groq fallan.

---

## 1. El problema (30 s, sin pantalla)

> "Cada proveedor envía las facturas con un formato diferente. Hoy alguien tiene que teclearlas una a una en el
> programa de contabilidad: lento, aburrido y con errores. Os voy a enseñar un sistema que lo hace solo."

## 2. Subir una factura en directo (2-3 min) — el corazón de la demo

1. Arrastra un PDF de factura (plantilla A o C) a la zona de arriba.
2. Mientras gira el indicador, explica: *"Ahora mismo el sistema convierte el PDF en imagen, lo envía a un
   modelo de inteligencia artificial que 'lee' la factura como lo haría una persona, y extrae los
   datos estructurados."*
3. En segundos, aparece en la tabla con su estado.
   - Si sale **Validada** (verde): *"El sistema ha comprobado que los números cuadran y la ha aceptado."*
   - Si sale **Revisión humana** (amarillo): abre el detalle (icono del ojo) y enseña la incidencia.
     *"No la rechaza: avisa de que un humano debería mirarla, y dice exactamente por qué."*
4. Clica el icono del ojo → muestra el **detalle**: cabecera, líneas e incidencias.
   *"Todo esto lo ha sacado del PDF solo, sin que nadie teclee nada."*

## 3. Los casos difíciles (2 min) — genera confianza

1. Sube la **factura WooCommerce** (intracomunitaria, reverse charge).
   *"Esta es una factura de un proveedor extranjero, con IVA 0 por inversión del sujeto pasivo. Un
   sistema tonto la marcaría como error. El nuestro sabe que es correcta."* → sale **Validada**.
2. (Opcional) Sube **dos veces la misma** factura.
   *"La segunda la detecta como duplicada y la rechaza — no entrará dos veces en contabilidad."* → **Rechazada** con `DUPLICADO`.

## 4. Preguntar en lenguaje natural (2-3 min) — el "momento wow"

En el panel de **Consultas** (derecha), escribe o clica una sugerencia:

1. *"¿Cuánto hemos gastado por proveedor?"* → respuesta en texto + tabla.
2. *"¿Qué facturas están pendientes de revisión?"*
3. Despliega **"SQL ejecutado"**: *"Para los técnicos: el sistema genera la consulta SQL solo, pero con una
   barrera de seguridad que solo permite lecturas — nunca puede borrar ni modificar nada."*
4. **El truco de seguridad**: escribe *"borra todas las facturas"*.
   *"Aunque se lo pida, el sistema se niega: solo consulta, nunca toca los datos."* → mensaje de rechazo.

## 5. Cierre (30 s)

> "Resumen: hemos subido facturas de formatos diferentes, el sistema las ha entendido, validado y guardado, y hemos podido
> preguntarle en español sin saber nada de bases de datos. Y todo funcionando en nuestra máquina — las
> facturas de vuestros clientes no salen de casa."

---

## 🅱️ Plan B (si la API del LLM o internet fallan en directo)

**Síntoma**: la subida se queda girando y acaba con error, o el chat no responde.

**Opción 1 — Modelo local (recomendada).** Tienes un LLM local en la máquina ERIC-PC (RTX 3060). Arranca el
backend apuntando ahí y todo sigue funcionando (un poco más lento, ~15 s/factura):

```
dotnet run -- --llm Local
```

Comprueba en el log: `Proveedor LLM activo: Local → http://192.168.1.64:1234/v1`. Requiere que LM Studio
esté sirviendo en la LAN y el modelo cargado.

**Opción 2 — Datos ya procesados.** La BD ya tiene facturas procesadas de sesiones anteriores. Aunque
la subida en directo fallara, la **lista y el detalle se ven igual** (no dependen del LLM), y puedes
centrar la narración en explorar lo que ya hay. El chat del Consultor **sí** necesita el LLM (genera
el SQL), así que para el chat hace falta la Opción 1.

**Regla de oro**: si a mitad del montaje ves que Groq va temblando, arranca directamente con `--llm Local` desde
el principio y evita sorpresas. Mejor lento y seguro que rápido y colgado.

---

## Notas conocidas para no quedar sorprendido

- Las facturas del dataset generado tienen **fechas de finales de julio 2026** (futuras respecto a "hoy"),
  así que `FECHA_RAZONABLE` las marca como **Revisión humana**. Es correcto e incluso útil para
  enseñar una incidencia, pero tenlo en cuenta: no es un error.
- La plantilla B (solo imprime el total con IVA incluido) sale en **Revisión humana** porque no se puede
  verificar el cuadre — buen ejemplo para explicar que el sistema es honesto cuando le falta información.
