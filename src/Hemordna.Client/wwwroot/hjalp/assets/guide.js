/* Hemordna - delad mall för användarguiderna under /hjalp.

   Varje guidesida (sv/*.html) är ett tunt skal som bara definierar window.GUIDE och laddar den
   här filen sist. All layout - sidopanel, innehållsförteckning, kapitel, sidfot - byggs här, så
   en ny guide bara bär sitt innehåll och aldrig sin egen struktur. Samma uppdelning som
   BowlingPlatforms guider använder.

   window.GUIDE = {
     title:    "För alla i familjen",
     subtitle: "Användarguide",
     intro:    "En mening om vem guiden är för.",
     fakta:    ["18 skärmbilder", "Tagna på 390 x 844"],   // valfri
     sections: [ { id: "idag", t: "Din dag", html: "<p>...</p>" }, ... ]
   }

   `html` är förförfattad HTML från vår egen källfil, aldrig användarinmatning - den sätts med
   innerHTML med flit, och inget på sidan kommer utifrån. */

(function () {
    "use strict";

    var guide = window.GUIDE;

    if (!guide || !Array.isArray(guide.sections)) {
        return;
    }

    function el(tag, className, text) {
        var node = document.createElement(tag);
        if (className) { node.className = className; }
        if (text) { node.textContent = text; }
        return node;
    }

    document.title = guide.title + " - Hemordna";

    var layout = el("div", "layout");

    // Sidopanel med innehållsförteckning.
    var aside = el("aside");
    var marke = el("p", "marke");
    var markeLank = el("a", null, "Hemordna");
    markeLank.href = "../index.html";
    marke.appendChild(markeLank);
    aside.appendChild(marke);
    aside.appendChild(el("p", "marke-under", guide.title));

    var toc = el("nav", "toc");
    toc.setAttribute("aria-label", "Innehåll");
    aside.appendChild(toc);
    layout.appendChild(aside);

    // Huvudinnehåll.
    var main = el("main");
    main.appendChild(el("p", "eyebrow", guide.subtitle || "Användarguide"));
    main.appendChild(el("h1", null, guide.title));

    if (guide.intro) {
        main.appendChild(el("p", "lead", guide.intro));
    }

    if (Array.isArray(guide.fakta) && guide.fakta.length) {
        var fakta = el("div", "fakta");
        guide.fakta.forEach(function (text) { fakta.appendChild(el("span", null, text)); });
        main.appendChild(fakta);
    }

    guide.sections.forEach(function (section, index) {
        var wrapper = el("section", "kapitel");
        wrapper.id = section.id;

        var topp = el("div", "kapitel-topp");
        topp.appendChild(el("span", "steg-nr", "Steg " + (index + 1)));
        topp.appendChild(el("h2", null, section.t));
        wrapper.appendChild(topp);

        var body = document.createElement("div");
        body.innerHTML = section.html;
        wrapper.appendChild(body);

        main.appendChild(wrapper);

        var link = el("a", null, section.t);
        link.href = "#" + section.id;
        toc.appendChild(link);
    });

    if (guide.footer) {
        var footer = el("footer");
        footer.innerHTML = guide.footer;
        main.appendChild(footer);
    }

    layout.appendChild(main);
    document.body.appendChild(layout);

    // En tabell som ändå inte får plats ska rulla i sin egen ruta i stället för att dra ut hela
    // sidan i sidled på en telefon - samma fälla BowlingPlatforms guider redan gått i.
    Array.prototype.forEach.call(main.querySelectorAll("table"), function (table) {
        if (table.parentNode && table.parentNode.classList.contains("tabell-wrap")) {
            return;
        }
        var wrap = el("div", "tabell-wrap");
        table.parentNode.insertBefore(wrap, table);
        wrap.appendChild(table);
    });

    // Markera var i guiden man befinner sig.
    var links = Array.prototype.slice.call(toc.querySelectorAll("a"));
    var sections = links
        .map(function (a) { return document.getElementById(a.getAttribute("href").slice(1)); })
        .filter(Boolean);

    if (!sections.length || !("IntersectionObserver" in window)) {
        return;
    }

    function setActive(id) {
        links.forEach(function (a) {
            a.classList.toggle("aktiv", a.getAttribute("href") === "#" + id);
        });
    }

    setActive(sections[0].id);

    var observer = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
            if (entry.isIntersecting) { setActive(entry.target.id); }
        });
    }, { rootMargin: "-10% 0px -70% 0px", threshold: 0 });

    sections.forEach(function (section) { observer.observe(section); });
})();
