melee-inject-failed-hardsuit = Ваш { $weapon } не может проникнуть сквозь скафандр!
melee-balloon-pop = { CAPITALIZE($balloon) } лопнул!
melee-weapon-dealt-no-damage = { CAPITALIZE(THE($weapon)) } не наносит урон: { THE($target) }!
melee-self-weapon-dealt-no-damage = Вы не наносите урон: { THE($target) }!
# MeleeBatteryHitsLeftSystem
examine-battery-hits-left = Заряда хватит ещё на [color={ $color }]{ $count }[/color] ударов.
# BatteryComponent
melee-battery-examine =
    Заряда хватит на [color={ $color }]{ $count }[/color] { $count ->
        [one] удар
        [few] удара
       *[other] ударов
    }.
