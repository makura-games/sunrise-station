shared-solution-container-component-on-examine-main-text =
    Оно содержит [color={ $color }]{ $desc }[/color] { $chemCount ->
        [1] вещество.
       *[other] смесь веществ.
    }
examinable-solution-has-recognizable-chemicals = Вы узнаёте в составе: { $recognizedString }.
examinable-solution-recognized = [color={ $color }]{ $chemical }[/color]
examinable-solution-on-examine-volume =
    Контейнер { $fillLevel ->
        [exact] содержит [color=white]{ $current }/{ $max }ед[/color].
       *[other] [bold]{ -solution-vague-fill-level(fillLevel: $fillLevel) }[/bold].
    }
examinable-solution-on-examine-volume-no-max =
    В контейнере { $fillLevel ->
        [exact] содержится [color=white]{ $current }ед[/color].
       *[other] [bold]{ -solution-vague-fill-level(fillLevel: $fillLevel) }[/bold].
    }
examinable-solution-on-examine-volume-puddle =
    Лужа { $fillLevel ->
        [exact] [color=white]{ $current }u[/color].
        [full] огромная и переливается!
        [mostlyfull] огромная и переливается!
        [halffull] глубокая и растекается.
        [halfempty] очень глубокая.
       *[mostlyempty] собирается в пятна.
        [empty] распалась на мелкие капли.
    }
-solution-vague-fill-level =
    { $fillLevel ->
        [full] [color=white]полный[/color]
        [mostlyfull] [color=#DFDFDF]почти полный[/color]
        [halffull] [color=#C8C8C8]наполовину полный[/color]
        [halfempty] [color=#C8C8C8]наполовину пустой[/color]
        [mostlyempty] [color=#A4A4A4]почти пустой[/color]
       *[empty] [color=gray]пустой[/color]
    }
