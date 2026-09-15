## Base actions

alerts-vampire-blood-name = Кровное опьянение
alerts-vampire-blood-desc = Показывает, сколько крови вы выпили. Вытяните клыки и кликните левой кнопкой по цели, чтобы напиться.

alerts-vampire-fed-name = Сытость
alerts-vampire-fed-desc = Ваша текущая сытость. Пейте кровь, чтобы оставаться сытым.

roles-antag-vampire-name = Вампир
roles-antag-vampire-description = Питайтесь экипажем. Вытяните клыки и пейте их кровь.

roles-antag-thrall-name = Тхралл
roles-antag-thrall-objective = Слушайтесь своего господина и выполняйте его приказы.

vampire-roundend-name = вампир

vampire-drink-start = Вы вонзаете клыки в {CAPITALIZE(THE($target))}.

vampire-not-enough-blood = Недостаточно крови.

vampire-mouth-covered = Ваш рот закрыт!
vampire-drink-invalid-target = Вы не можете пить кровь вампиров или их тхраллов.
vampire-target-protected-by-faith = Этот человек защищён своей верой!
vampire-drink-target-empty = В этом существе нет крови!
vampire-drink-target-maxed = Вы уже выпили { $amount } единиц крови из этой цели.
vampire-drink-target-hard-max = Вы выпили максимальное количество крови из этой цели ({ $amount } единиц).
vampire-full-power-achieved = Ваша вампирская сущность достигла полной силы!
vampire-umbrae-full-power-fov = Тени подчиняются вашей воле. Вы теперь видите сквозь стены!
vampire-drink-target-not-viable = У этого существа нет бьющегося сердца!
vampire-drink-target-rot = Сущность этого существа гнилая!
vampire-sleep-shielded = Это существо нельзя усыпить из-за импланта!
vampire-sleep-protected = Нужен зрительный контакт...

vampire-role-greeting = Вы — вампир!
    Ваша жажда крови заставляет вас питаться членами экипажа. Используйте свои способности, чтобы обращать других.
    Ваши клыки позволяют высасывать кровь из людей. Кровь восстанавливает здоровье и даёт новые способности.
    Найдите, чем заняться в эту смену!

# Objectives
objective-issuer-vampire = [color=crimson]Вампир[/color]

objective-condition-drain-title = Выпить {$count} единиц крови
objective-condition-drain-description = Выпейте {$count} единиц крови у членов экипажа, используя свои клыки.

objective-vampire-thrall-obey-master-title = Слушайтесь своего господина, {$targetName}.

# Class selection action
action-vampire-class-select = Выбор класса вампира
action-vampire-class-select-desc = Выберите свой подкласс вампира

# Round end statistics
roundend-prepend-vampire-drained-low = Вампиры едва питались в эту смену, выпив всего {$blood} единиц крови.
roundend-prepend-vampire-drained-medium = Вампиры неплохо поели, выпив {$blood} единиц крови.
roundend-prepend-vampire-drained-high = Вампиры устроили пир крови, выпив {$blood} единиц крови!
roundend-prepend-vampire-drained-critical = Вампиры устроили кровавую оргию, выпив ошеломляющие {$blood} единиц крови!

roundend-prepend-vampire-drained = Вампирам не удалось выпить сколько-нибудь значимое количество крови в этом раунде.
roundend-prepend-vampire-drained-named = {$name} был самым кровожадным вампиром, выпив в сумме {$number} единиц крови.

# Vampire class selection tooltips
vampire-class-hemomancer-tooltip = Класс крови
    Hemomancer — это боевой вампир, способный управлять кровью.
vampire-class-umbrae-tooltip = Класс тьмы
    Umbrae — скрытный вампир, использующий тени.
vampire-class-gargantua-tooltip = Класс силы
    Gargantua — это танк, способный выдерживать огромный урон.
vampire-class-dantalion-tooltip = Класс разума
    Dantalion — повелитель тхраллов, способный подчинять разум.

# Vampire fed alerts
vampire-fed-fat = Толстый
vampire-fed-starving = Вампир голоден
vampire-fed-hungry = Голоден
vampire-fed-fed = Сыт
vampire-fed-well-fed = Хорошо накормлен

# Blood actions
vampire-blood-eruption-activated = Кровавое извержение!
vampire-blood-barrier-wrong-place = Невозможно разместить барьер здесь.
vampire-blood-bond-start = Кровавая связь установлена.
vampire-blood-bond-stop = Кровавая связь разорвана.
vampire-blood-bond-stop-blood = Кровавая связь разорвана: недостаточно крови.
vampire-blood-bond-no-thralls = Нет тхраллов поблизости для связи.
vampire-blood-bringers-rite-start = Обряд Кровеносца активирован.
vampire-blood-bringers-rite-stop = Обряд Кровеносца деактивирован.
vampire-blood-bringers-rite-stop-blood = Обряд Кровеносца деактивирован: недостаточно крови.
vampire-blood-bringers-rite-not-enough-power = Недостаточно силы для Обряда Кровеносца.
vampire-blood-rush-start = Кровавый рывок!
vampire-blood-rush-end = Кровавый рывок закончился.
vampire-blood-swell-start = Кровавый наплыв!
vampire-blood-swell-end = Кровавый наплыв закончился.
vampire-blood-swell-cancel-shoot = Кровавый наплыв отменён: выстрел невозможен.
vampire-charge-start = Рывок!
vampire-charge-impact = Столкновение!
vampire-overwhelming-force-no-target = Нет цели.
vampire-overwhelming-force-door-pry = Взлом двери ({$bloodCost} крови)
vampire-predator-sense-cooldown = Чутьё хищника перезаряжается.
vampire-shadow-boxing-end = Теневые боксёры рассеялись.
vampire-shadow-boxing-swarm = Рой атакует!
vampire-shadow-boxing-no-target = Нет целей поблизости.
vampire-shadow-boxing-too-far = Вы слишком далеко от роя.

# Vampire action errors
action-vampire-not-enough-power = Недостаточно силы для этой способности.
action-vampire-not-enough-blood = Недостаточно крови для этой способности.
action-vampire-class-required = Требуется класс: {$class}.

# Fangs
vampire-fangs-extended = Вы вытягиваете свои клыки
vampire-fangs-retracted = Вы втягиваете свои клыки

# Preset
vampire-preset-title = Вампир
vampire-preset-description = Вампиры

# Role subtype
role-subtype-vampire = Вампир

# Legs ensnared
vampire-legs-ensnared = Ваши ноги в ловушке!

ent-ActionVampireToggleFangs = Клыки
    .desc = Вытяните или уберите клыки, чтобы пить кровь жертв.
ent-ActionVampireGlare = Взгляд
    .desc = Уставьтесь на ближайшие цели, парализуя и заставляя их замолчать.
ent-ActionVampireSleep = Сон (15)
    .desc = Отправить жертву во временный сон.
ent-ActionVampireRejuvenateI = Восстановление
    .desc = Снимает любые оглушения и восстанавливает выносливость.
ent-ActionVampireRejuvenateII = Восстановление II
    .desc = Снимает любые оглушения и восстанавливает выносливость. Выводит яды и лечит вас со временем.
ent-ActionClassSelectId = Выбор класса вампира
    .desc = Выберите свой подкласс вампира.
ent-ActionVampireHemomancerClaws = Кровавые когти (30)
    .desc = Превратите руки в кровавые когти. Высасывайте кровь с каждым ударом. Когти исчезнут после нескольких успешных выпиваний или активного использования.
ent-ActionVampireSanguinePool = Кровавая лужа (30)
    .desc = Растопитесь в лужу крови, позволяя проскользнуть под двери и окна.
ent-ActionVampireHemomancerTendrils = Кровавые щупальца (25)
    .desc = Щупальца вырываются квадратом, отравляя и замедляя жертв.
ent-ActionVampireBloodBarrier = Кровавый барьер (40)
    .desc = Создайте линию кровавых барьеров в указанном месте. Пройти можете только вы.
ent-ActionVampirePredatorSense = Чутьё хищника (20)
    .desc = Выследите свою жертву, ей негде спрятаться...
ent-ActionVampireBloodEruption = Кровавое извержение (100)
    .desc = Вызовите вокруг себя извергающиеся лужи крови, наносящие урон.
ent-ActionVampireBloodBringersRite = Обряд Кровеносца (10/2с)
    .desc = Включите разрушительную ауру, высасывающую жизненную силу из жертв поблизости для исцеления.
ent-ActionVampireCloakOfDarkness = Плащ тьмы
    .desc = Включите скрытность, сильнейшую в темноте и слабейшую при ярком свете. Мобы поблизости раскрывают вас.
ent-ActionVampireShadowSnare = Теневая ловушка (20)
    .desc = Установите вредоносную теневую ловушку для жертв. Ловушка замедляет и ослепляет вспышкой.
ent-ActionVampireShadowAnchor = Теневой якорь (20)
    .desc = Установите теневой якорь под ногами. Активируйте снова, чтобы вернуться к якорю, или вас призовёт автоматически.
ent-ActionVampireShadowBoxing = Теневые боксёры (50)
    .desc = Прикажите своим теневым летучим мышам атаковать. Рой рассеется, если вы отойдёте слишком далеко.
ent-ActionVampireDarkPassage = Тёмный проход (20)
    .desc = Телепортируйтесь в указанное место через тени.
ent-ActionVampireExtinguish = Погасить свет
    .desc = Разбейте ближайшие работающие светильники.
ent-ActionVampireEternalDarkness = Вечная тьма
    .desc = Включите, чтобы окутать область вокруг вас тьмой; вы излучаете неестественный холод.
ent-ActionVampireEnthrall = Порабощение (150)
    .desc = Направьте поток, чтобы подчинить гуманоидную цель своей воле. Движение вас или цели разорвёт связь.
ent-ActionVampirePacify = Умиротворение (30)
    .desc = Заполните разум жертвы блаженством, умиротворив её.
ent-ActionVampireSubspaceSwap = Подпространственный обмен (30)
    .desc = Поменяйтесь местами со смертным. Жертва замедляется и сходит с ума.
ent-ActionVampireDecoy = Приманка (30)
    .desc = Создайте приманку, позволяющую вам стать невидимым. Приманка ослепляет атакующих при уроне.
ent-ActionVampireRallyThralls = Призыв тхраллов (100)
    .desc = Прикажите тхраллам стряхнуть оглушения, проснуться и восстановить выносливость.
ent-ActionVampireBloodBond = Кровавая связь (2.5/1с)
    .desc = Включите, чтобы прикрепить кровавые нити к видимым тхраллам поблизости, распределяя входящий урон по группе.
ent-ActionVampireMassHysteria = Массовая истерия (70)
    .desc = Заполните всех гуманоидов поблизости, не являющихся тхраллами, ужасом, вызывая истерию.
ent-ActionVampireBloodSwell = Кровавый наплыв (30)
    .desc = Войдите в состояние, затвердевающее вашу кожу. Нельзя использовать оружие. При (400) крови наносите бонусный урон в ближнем бою.
ent-ActionVampireBloodRush = Кровавый рывок (30)
    .desc = На ограниченное время бегите с неестественной скоростью.
ent-ActionVampireSeismicStomp = Сейсмический топот (30)
    .desc = Ударьте по земле, отбрасывая всех существ от вас.
ent-ActionVampireOverwhelmingForce = Сокрушающая сила
    .desc = При включении вас нельзя толкнуть, сдвинуть или потянуть. Вы автоматически взламываете запитанные двери за кровь. (15)
ent-ActionVampireDemonicGrasp = Демоническая хватка (20)
    .desc = Запустите демоническую руку, обездвиживающую цель. В боевом режиме также притягивает её к вам.
ent-ActionVampireCharge = Рывок (30)
    .desc = Неситесь, пока не столкнётесь с препятствием или пустотой. Наносит тяжёлый урон и отбрасывает существ, сокрушая стены и конструкции.
ent-VampireBloodTendrilVisual = Кровавые щупальца
    .desc = Кровавые щупальца, вырывающиеся из земли.
ent-VampireShadowBoxingPunch = Теневой удар
    .desc = Удар теневой летучей мыши.
ent-VampireBloodBarrier = Кровавый барьер
    .desc = Барьер из затвердевшей крови, блокирующий движение.
ent-VampireSanguinePoolOut = Выход из лужи
ent-VampireSanguinePoolIn = Вход в лужу
ent-VampireBloodEruptionVisual = Кровавое извержение
ent-VampireDrainBeam = Луч высасывания
    .desc = Багровый луч, высасывающий жизненную энергию.
ent-VampireDrainBeamVisual = Луч высасывания
    .desc = Плавный клиентский луч высасывания вампира.
ent-VampireShadowAnchorBeacon = Теневой якорь
    .desc = Пульсирующий узел тьмы, к которому можно вернуться.
ent-VampireShadowSnare = Теневая ловушка
    .desc = Почти невидимая ловушка из сгустившихся теней.
ent-VampireShadowSnareEnsnare = Теневые путы
    .desc = Тёмные щупальца, связывающие ваши ноги.
ent-MobVampireSanguinePool = Кровавая лужа
    .desc = Разумная лужа вампирской крови.
ent-StatusEffectVampireBloodSwell = Кровавый наплыв
ent-StatusEffectVampireBloodRush = Кровавый рывок
ent-VampireDecoyEntity = Приманка вампира
ent-VampireSurviveObjective = Выжить
    .desc = Я должен выжить любой ценой.
ent-VampireEscapeObjective = Сбежать на ЦК живым и не связанным
    .desc = Мне нужно сбежать на эвакуационном шаттле. Инкогнито.
ent-VampireThrallObeyMasterObjective = Повинуйтесь своему господину
    .desc = Вы порабощены. Следуйте приказам господина.

alerts-vampire-blood-swell-name = Кровавый наплыв
alerts-vampire-blood-swell-desc = Ваши мышцы наливаются нечестивой силой.

alerts-vampire-blood-rush-name = Кровавый рывок
alerts-vampire-blood-rush-desc = Сверхъестественная скорость течёт по вашим жилам.

Vamp-converted-confirm = Подтвердить
Vamp-converted-text = Этот человек обратился в вампира.
Vamp-converted-title = Обращён в вампира
action-vampire-blood-barrier-wrong-place = Здесь нельзя создать кровавый барьер.
action-vampire-blood-brighters-rite-not-enough-blood = Недостаточно крови для обряда Кровеносца.
action-vampire-blood-bringers-rite-not-enough-power = Недостаточно силы для обряда Кровеносца.
action-vampire-blood-bringers-rite-start = Вы начинаете обряд Кровеносца.
action-vampire-blood-bringers-rite-stop = Вы прекращаете обряд Кровеносца.
action-vampire-blood-bringers-rite-stop-blood = Недостаточно крови для поддержания обряда.
action-vampire-blood-eruption-activated = Кровь вокруг вас взрывается!
action-vampire-cloak-of-darkness-start = Вы укутываетесь в плащ тьмы.
action-vampire-cloak-of-darkness-stop = Плащ тьмы спадает.
action-vampire-dark-passage-activated = Вы скользите сквозь тени.
action-vampire-dark-passage-wrong-place = Здесь нельзя использовать тёмный проход.
action-vampire-eternal-darkness-not-enough-blood = Недостаточно крови для вечной тьмы.
action-vampire-eternal-darkness-start = Вечная тьма сгущается вокруг вас.
action-vampire-eternal-darkness-stop = Вечная тьма рассеивается.
action-vampire-extinguish-activated = Огни вокруг гаснут!
action-vampire-hemomancer-tendrils-wrong-place = Здесь нельзя создать щупальца.
action-vampire-sanguine-pool-already-in = Вы уже в кровавой луже.
action-vampire-sanguine-pool-enter = Вы растворяетесь в кровавой луже.
action-vampire-sanguine-pool-exit = Вы выходите из кровавой лужи.
action-vampire-sanguine-pool-invalid-tile = Здесь нельзя растечься лужей.
action-vampire-shadow-anchor-installed = Теневой якорь установлен.
action-vampire-shadow-anchor-returned = Вы возвращаетесь к теневому якорю.
action-vampire-shadow-boxing-ends = Теневые боксёры исчезают.
action-vampire-shadow-boxing-start = Теневые боксёры атакуют!
action-vampire-shadow-boxing-stop = Вы прекращаете теневой бокс.
action-vampire-shadow-snare-placed = Теневая ловушка установлена.
action-vampire-shadow-snare-scatter = Теневая ловушка рассеивается.
action-vampire-shadow-snare-wrong-place = Здесь нельзя установить теневую ловушку.
ent-shadow-snare-ensnare = Теневые путы
predator-sense-title = Чутьё хищника
vampire-demonic-grasp-hit = Демоническая хватка задевает цель!
vampire-demonic-grasp-pull = Демоническая хватка тянет цель к вам!
vampire-enthrall-invalid = Нельзя поработить эту цель.
vampire-enthrall-limit = Вы не можете поработить больше тхраллов.
vampire-enthrall-start = Вы начинаете порабощение...
vampire-enthrall-success = Цель порабощена!
vampire-enthrall-target = Вас порабощают...
vampire-holy-place-burn = Святое место обжигает вас!
vampire-locate-no-targets = Цели не найдены.
vampire-locate-not-same-sector = Цель не в вашем секторе.
vampire-locate-result = Цель обнаружена
vampire-locate-search-placeholder = Поиск...
vampire-locate-unknown = Неизвестно
vampire-overwhelming-force-door-pried = Вы взламываете дверь!
vampire-overwhelming-force-start = Сокрушительная сила активирована.
vampire-overwhelming-force-stop = Сокрушительная сила деактивирована.
vampire-overwhelming-force-too-heavy = Цель слишком тяжёлая.
vampire-pacify-invalid = Нельзя умиротворить эту цель.
vampire-pacify-success = Цель умиротворена.
vampire-pacify-target = Вас умиротворяют...
vampire-rally-thralls-none = У вас нет тхраллов.
vampire-rally-thralls-success = Ваши тхраллы пробуждены!
vampire-seismic-stomp-activate = Сейсмический топот!
vampire-shadow-snare-oldest-removed = Старейшая теневая ловушка убрана.
vampire-space-burn-warning = Вакуум космоса обжигает вашу сущность!
vampire-subspace-swap-dead = Нельзя обменяться с мёртвой целью.
vampire-subspace-swap-failed = Обмен не удался.
vampire-subspace-swap-success = Вы меняетесь местами!
vampire-subspace-swap-target = Кто-то меняется с вами местами!
vampire-subspace-swap-thrall = Нельзя обменяться с тхраллом.
vampire-thrall-holy-water-freed = Святая вода освобождает тхралла!
vampire-thrall-released = Тхралл освобождён.
vampiric-claws-remove-popup = Кровавые когти исчезают.
