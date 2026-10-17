status-effect-examine-adrenaline = [color=red]Всё { POSS-ADJ($target) } тело кажется напряжённым и настороженным.[/color]
status-effect-examine-drunk =
    { GENDER($target) ->
        [male] [color=brown]Он пьян...[/color]
        [female] [color=brown]Она пьяна...[/color]
        [epicene] [color=brown]Они пьяны...[/color]
       *[neuter] [color=brown]Оно пьяно...[/color]
    }
status-effect-examine-seeing-rainbow = [color=lightgreen]{ CAPITALIZE(SUBJECT($target)) } { CONJUGATE-BASIC($target, "глазеют", "глазеет") } на то, чего нет.[/color]
status-effect-examine-stunned = [color=yellow]{ CAPITALIZE(POSS-ADJ($target)) } тело выглядит изнурённым и неспособным двигаться.[/color]
status-effect-examine-temporary-blindness = [color=lightblue]{ CAPITALIZE(POSS-ADJ($target)) } глаза расфокусировались. Похоже, { SUBJECT($target) } плохо видит.[/color]
