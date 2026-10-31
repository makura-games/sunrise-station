### UI

# Displayed in the Character prefs window
humanoid-character-profile-summary =
    Это { $name }. { $gender ->
        [male] Ему
        [female] Ей
        [epicene] Им
       *[neuter] Ему
    } { $age } { $age ->
        [one] год
        [few] года
       *[other] лет
    }.

humanoid-character-profile-summary-species-sex =
    { $gender ->
        [male] Он
        [female] Она
        [epicene] Они
       *[neuter] Оно
    } { $species } { $sex ->
        [male] мужского пола
        [female] женского пола
       *[other] неизвестного пола
    }.
humanoid-character-profile-summary-dream-job = Желаемая должность: { $job }.

