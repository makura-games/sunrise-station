## Классы болезней

disease-classification-viral = вирусная
disease-classification-bacterial = бактериальная
disease-classification-fungal = грибковая
disease-classification-parasitic = паразитарная
disease-classification-environmental = экологическая
disease-classification-anomalous = аномальная
disease-classification-unknown = неизвестная

## Синдромы

disease-syndrome-unknown = неопределённый синдром
disease-syndrome-respiratory = респираторный синдром

## Симптомы

disease-symptom-sore-throat = боль в горле
disease-symptom-sore-throat-popup = У вас першит в горле.
disease-symptom-cough = кашель
disease-symptom-sneeze = чихание
disease-symptom-fever = лихорадка
disease-symptom-fever-onset = Вас бросает в жар.
disease-symptom-nausea = тошнота
disease-symptom-nausea-onset = Вас начинает мутить.
disease-symptom-toxemia = интоксикация

## Цели лечебных эффектов

disease-effect-target-all = все болезни
disease-effect-target-class = болезни класса «{ $class }»
disease-effect-target-list = { $diseases }

## Справочник реагентов

entity-effect-guidebook-disease-cure =
    { $chance ->
        [1] Лечит
       *[other] лечат
    } { $target }
entity-effect-guidebook-disease-suppress =
    { $chance ->
        [1] Подавляет
       *[other] подавляют
    } { $target } на { NATURALFIXED($time, 3) } { MANY("second", $time) }
entity-effect-guidebook-hair-loss =
    { $chance ->
        [1] Вызывает
       *[other] вызывают
    } выпадение волос

## Мазок

disease-swab-examine-used = Мазок [color=yellow]использован[/color].
disease-swab-examine-unused = Мазок [color=green]стерилен[/color].
disease-swab-popup-used = Этот мазок уже использован.
disease-swab-popup-taken = Вы взяли образец с { $target }.
disease-symptom-headache = головная боль
disease-symptom-headache-onset = В висках начинает ломить.
disease-symptom-rash = сыпь
disease-symptom-rash-onset = На коже проступают красные пятна.
disease-symptom-rash-popup = Сыпь нестерпимо зудит.
disease-symptom-muscle-ache = ломота в теле
disease-symptom-muscle-ache-onset = Мышцы наливаются тяжестью.
disease-symptom-fatigue = упадок сил
disease-symptom-chills = озноб
disease-symptom-dizziness = головокружение
disease-symptom-blurred-vision = помутнение зрения
disease-symptom-nosebleed = носовое кровотечение
disease-symptom-nosebleed-onset = Из носа начинает идти кровь.
disease-symptom-anemia = малокровие
disease-symptom-hallucinations = галлюцинации
disease-symptom-hallucinations-onset = Тени по углам начинают шевелиться.
disease-symptom-hyperthermia = гипертермия
disease-symptom-suffocation = удушье
disease-symptom-luminescence = свечение кожи
disease-symptom-luminescence-onset = Кожа начинает тускло светиться.
disease-symptom-regeneration = ускоренная регенерация
disease-symptom-regeneration-onset = Ссадины затягиваются прямо на глазах, а в животе урчит от голода.
disease-symptom-adrenaline = приливы адреналина
disease-symptom-adrenaline-popup = Сердце колотится, ноги сами несут вперёд.
disease-symptom-efficient-metabolism = экономный обмен веществ
disease-symptom-efficient-metabolism-onset = Голод и жажда будто отступают.
disease-symptom-endurance = неутомимость
disease-symptom-endurance-onset = Усталость как рукой сняло, только во рту пересохло.
disease-symptom-detoxification = детоксикация
disease-symptom-detoxification-onset = Тяжесть в боку сменяется странной лёгкостью.
disease-symptom-capacious-lungs = ёмкие лёгкие
disease-symptom-capacious-lungs-onset = Каждый вдох кажется глубже обычного.
disease-symptom-tricordrazine-synthesis = синтез трикордразина
disease-symptom-tricordrazine-synthesis-onset = Ссадины ноют меньше, зато всё время хочется есть.
disease-symptom-nil = нуль-синдром
disease-symptom-waiting = синдром ожидания
disease-symptom-itch = зуд
disease-symptom-itch-popup-1 = Кожа нестерпимо чешется.
disease-symptom-itch-popup-2 = Вы расчёсываете зудящее место.
disease-symptom-phlegm = мокрота
disease-symptom-phlegm-popup = В горле скапливается слизь.
disease-symptom-salivation = слюнотечение
disease-symptom-salivation-popup = Рот наполняется слюной.
disease-symptom-twitch = подёргивания
disease-symptom-laughter = приступы смеха
disease-symptom-screaming = приступы крика
disease-symptom-honking = хонк-синдром
disease-symptom-hair-loss = облысение
disease-symptom-hair-loss-onset = Волосы лезут клочьями.
disease-symptom-voracious-appetite = волчий аппетит
disease-symptom-voracious-appetite-onset = Вас мучает зверский голод.
disease-symptom-pacifism = апатия
disease-symptom-pacifism-onset = Драться совершенно не хочется.
disease-symptom-gas-synthesis = газообразование
disease-symptom-gas-synthesis-popup = Отчётливо пахнет чем-то кислым.
disease-symptom-bee-cough = пчелиный кашель
disease-symptom-bee-cough-popup = Вы выкашливаете живую пчелу!
disease-symptom-histamine-synthesis = синтез гистамина
disease-symptom-unstable-nucleotide = нестабильный нуклеотид
disease-symptom-narcolepsy = нарколепсия
disease-symptom-narcolepsy-onset = Веки наливаются свинцом.
disease-symptom-blackout = приступы слепоты
disease-symptom-blackout-popup = В глазах темнеет!
disease-symptom-seizures = судороги
disease-symptom-cellular-decay = распад клеток
disease-symptom-radiation-sickness = лучевая болезнь
disease-symptom-monkism = обезьяний синдром
disease-symptom-monkism-onset = Вас тянет к бананам, а слова даются всё труднее.

## Синдромы

disease-syndrome-digestive = кишечный синдром
disease-syndrome-febrile = лихорадочный синдром
disease-syndrome-dermal = кожный синдром
disease-syndrome-hemorrhagic = геморрагический синдром
disease-syndrome-parasitic = паразитарный синдром
disease-syndrome-anomalous = неклассифицируемый синдром
disease-syndrome-neurological = неврологический синдром

## Рекомендации в отчётах

disease-hint-rest = постельный режим и тёплое питьё
disease-hint-fluids = обильное питьё, следить за обезвоживанием
disease-hint-source = найти и уничтожить очаг заражения
disease-hint-mask = стерильные маски всему отделу
disease-hint-isolation = изолировать заболевших немедленно
disease-hint-no-suppressants = супрессанты бесполезны, нужна вакцина

## Болезни

disease-space-cold-name = космическая простуда
disease-space-cold-description = Безобидный вирус, который гуляет по станциям круглый год. Проходит сам.
disease-space-cold-onset = Вы чувствуете себя простывшим.
disease-mold-allergy-name = споровая аллергия
disease-mold-allergy-description = Реакция на плесень из тёмных углов. От человека к человеку не передаётся.
disease-gastroenteritis-name = гастроэнтерит
disease-gastroenteritis-description = Кишечная инфекция. Расходится через еду, грязные руки и общую посуду.
disease-influenza-name = станционный грипп
disease-influenza-description = Сезонный вирус. Берёт отдел за пару часов, если не носить маски.
disease-parasites-name = кишечные паразиты
disease-parasites-description = Личинки из плохо прожаренного мяса. Тихо высасывают силы и кровь.
disease-pneumonia-name = пневмония
disease-pneumonia-description = Бактерии в лёгких. Без лечения доводит до удушья.
disease-xeno-spores-name = ксеноспороз
disease-xeno-spores-description = Споры чужой флоры прорастают в коже и лёгких. Долгая инкубация, тяжёлый исход.
disease-hemorrhagic-name = геморрагическая лихорадка
disease-hemorrhagic-description = Вирус разрушает сосуды. Больной истекает кровью изнутри.
disease-anomalous-blight-name = аномальная порча
disease-anomalous-blight-description = Заражение неизвестной природы. Обычные супрессанты на него не действуют.
disease-roleplay-cold-name = лёгкая простуда
disease-roleplay-cold-description = Пустяковое недомогание. Не заразно, проходит само.
disease-roleplay-cold-onset = Нос заложен, в горле першит.
disease-clown-fever-name = клоунская лихорадка
disease-clown-fever-description = Безобидный, но шумный вирус: больной гудит, хохочет и дёргается. Проходит сам.
disease-clown-fever-onset = В носу щекочет, и очень хочется посигналить.
disease-mucus-bronchitis-name = слизистый бронхит
disease-mucus-bronchitis-description = Бактерии в бронхах. Мокрота и кашель, расходится по воздуху.
disease-bacterial-fermentation-name = бродильная диспепсия
disease-bacterial-fermentation-description = Бактерии из испорченной еды бродят в кишечнике. Мучает голод, больной выделяет газ.
disease-hive-fever-name = ульевая лихорадка
disease-hive-fever-description = Личинки в лёгких. Сначала зуд, потом больной выкашливает пчёл.
disease-apathy-virus-name = вирус апатии
disease-apathy-virus-description = Подавляет волю: больной не может драться и проваливается в сон.
disease-neurotropic-fever-name = нейротропная лихорадка
disease-neurotropic-fever-description = Вирус поражает нервную систему. Судороги, слепота и внезапный сон.
disease-radian-syndrome-name = синдром Радиана
disease-radian-syndrome-description = Облучённая ткань распадается, волосы выпадают. Заразу переносят руки и инструменты.
disease-simian-retrovirus-name = обезьяний ретровирус
disease-simian-retrovirus-description = Перестраивает геном носителя. В худшем случае превращает его в обезьяну.

## Объявления

station-event-disease-outbreak-announcement = Внимание: зафиксирована вспышка инфекционного заболевания. Экипажу рекомендуется обратиться в медотдел при первых симптомах.
station-event-disease-vessel-breach-announcement = Внимание: в вирусологической лаборатории нарушена герметичность сосуда с культурой. Экипажу рекомендуется надеть маски, избегать отсека и обратиться в медотдел при первых симптомах.
disease-zombie-name = некротический штамм
disease-zombie-description = Возбудитель обращения. Вакцина спасает укушенного, но обратившегося уже не возвращает.
disease-hint-zombie = вакцинировать до обращения, обратившихся изолировать
disease-designed-name = спроектированный штамм
disease-designed-description = Штамм, собранный вирусологом вручную: набор симптомов у него ровно такой, какой отобрали под микроскопом.

# Карточки симптомов в гайдбуке. Страница собирается из прототипов симптомов.