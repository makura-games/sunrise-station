### UI

# Displayed in the Character prefs window
humanoid-character-profile-summary = 
    This is {$name}. {$gender ->
    [male] He is
    [female] She is
    [epicene] They are
    *[other] It is
} {$age} years old.

humanoid-character-profile-summary-species-sex =
    { $gender ->
        [male] He is a
        [female] She is a
        [epicene] They are a
       *[neuter] It is a
    } {$species} ({$sex ->
        [male] Male
        [female] Female
       *[other] Unknown sex
    }).
humanoid-character-profile-summary-dream-job = Preferred job: {$job}.
