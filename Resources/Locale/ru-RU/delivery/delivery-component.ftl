delivery-recipient-examine = Это предназначено для { $recipient }, { $job }.
delivery-already-opened-examine = Это уже было открыто.
delivery-earnings-examine = Доставка этого принесет станции [color=yellow]{ $spesos }[/color] денег.
delivery-recipient-no-name = Безымянный
delivery-recipient-no-job = Неизвестно
delivery-unlocked-self = Вы разблокировали { $delivery } своим отпечатком пальца.
delivery-opened-self = Вы открыли { $delivery }.
delivery-unlocked-others = { CAPITALIZE($recipient) } разблокировал { $delivery } { POSS-ADJ($possadj) } отпечатком пальца.
delivery-opened-others = { CAPITALIZE($recipient) } открыл { $delivery }.
delivery-unlock-verb = Разблокировать
delivery-open-verb = Открыть
delivery-slice-verb = Открыто
delivery-teleporter-amount-examine =
    { $amount ->
        [one] Содержит [color=yellow]{ $amount }[/color] посылку.
       *[other] Содержит [color=yellow]{ $amount }[/color] посылок.
    }
delivery-teleporter-empty = { $entity } пуст.
delivery-teleporter-empty-verb = заберите посылки
# modifiers
delivery-priority-examine = [color=orange]В ПРИОРИТЕТЕ![/color]. У вас осталось [color=orange]{ $time }[/color] чтобы получить бонус.
delivery-priority-expired-examine = [color=orange]В ПРИОРИТЕТЕ![/color]. Кажется, у вас закончилось время..
delivery-fragile-examine = [color=red]ОСТОРОЖНО ХРУПКОЕ![/color]. Принесите в сохраности чтобы получить бонус.
delivery-fragile-broken-examine = [color=red]ОСТОРОЖНО ХРУПКОЕ![/color]. Кажется там что-то уже разбилось...
delivery-priority-delivered-examine = Это [color=orange]приоритетная {$type}[/color]. Доставлено вовремя.

delivery-bomb-examine = Это [color=purple]{$type}-бомба[/color]. О нет.
delivery-bomb-primed-examine = Это [color=purple]{$type}-бомба[/color]. Читать это — пустая трата вашего времени.
