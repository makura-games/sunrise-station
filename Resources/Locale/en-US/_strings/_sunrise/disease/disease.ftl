## Disease classes

disease-classification-viral = viral
disease-classification-bacterial = bacterial
disease-classification-fungal = fungal
disease-classification-parasitic = parasitic
disease-classification-environmental = environmental
disease-classification-anomalous = anomalous
disease-classification-unknown = unknown

## Syndromes

disease-syndrome-unknown = unidentified syndrome
disease-syndrome-respiratory = respiratory syndrome

## Symptoms

disease-symptom-sore-throat = sore throat
disease-symptom-sore-throat-popup = Your throat feels scratchy.
disease-symptom-cough = cough
disease-symptom-sneeze = sneezing
disease-symptom-fever = fever
disease-symptom-fever-onset = You feel feverish.
disease-symptom-nausea = nausea
disease-symptom-nausea-onset = You start to feel queasy.
disease-symptom-toxemia = toxemia

## Treatment effect targets

disease-effect-target-all = all diseases
disease-effect-target-class = { $class } diseases
disease-effect-target-list = { $diseases }

## Reagent guidebook

entity-effect-guidebook-disease-cure =
    { $chance ->
        [1] Treats
        *[other] treat
    } { $target }
entity-effect-guidebook-disease-suppress =
    { $chance ->
        [1] Suppresses
        *[other] suppress
    } { $target } for { NATURALFIXED($time, 3) } { MANY("second", $time) }
entity-effect-guidebook-hair-loss =
    { $chance ->
        [1] Causes
        *[other] cause
    } hair loss

## Swab

disease-swab-examine-used = The swab has been [color=yellow]used[/color].
disease-swab-examine-unused = The swab is [color=green]sterile[/color].
disease-swab-popup-used = This swab has already been used.
disease-swab-popup-taken = You take a sample from { THE($target) }.
disease-symptom-headache = headache
disease-symptom-headache-onset = Your temples start to throb.
disease-symptom-rash = rash
disease-symptom-rash-onset = Red blotches spread across your skin.
disease-symptom-rash-popup = The rash itches unbearably.
disease-symptom-muscle-ache = muscle ache
disease-symptom-muscle-ache-onset = Your muscles grow heavy.
disease-symptom-fatigue = fatigue
disease-symptom-chills = chills
disease-symptom-dizziness = dizziness
disease-symptom-blurred-vision = blurred vision
disease-symptom-nosebleed = nosebleed
disease-symptom-nosebleed-onset = Your nose starts bleeding.
disease-symptom-anemia = anemia
disease-symptom-hallucinations = hallucinations
disease-symptom-hallucinations-onset = The shadows in the corners begin to move.
disease-symptom-hyperthermia = hyperthermia
disease-symptom-suffocation = suffocation
disease-symptom-luminescence = glowing skin
disease-symptom-luminescence-onset = Your skin begins to glow faintly.
disease-symptom-regeneration = accelerated regeneration
disease-symptom-regeneration-onset = Your scrapes knit shut before your eyes, and your stomach growls.
disease-symptom-adrenaline = adrenaline surges
disease-symptom-adrenaline-popup = Your heart pounds and your legs carry you forward.
disease-symptom-efficient-metabolism = efficient metabolism
disease-symptom-efficient-metabolism-onset = Hunger and thirst seem to recede.
disease-symptom-endurance = tirelessness
disease-symptom-endurance-onset = Your fatigue melts away, though your mouth feels dry.
disease-symptom-detoxification = detoxification
disease-symptom-detoxification-onset = The heaviness in your side gives way to a strange lightness.
disease-symptom-capacious-lungs = capacious lungs
disease-symptom-capacious-lungs-onset = Every breath feels deeper than usual.
disease-symptom-tricordrazine-synthesis = tricordrazine synthesis
disease-symptom-tricordrazine-synthesis-onset = Your aches dull, though you are always hungry.
disease-symptom-nil = nil syndrome
disease-symptom-waiting = waiting syndrome
disease-symptom-itch = itching
disease-symptom-itch-popup-1 = Your skin itches unbearably.
disease-symptom-itch-popup-2 = You scratch at an itchy spot.
disease-symptom-phlegm = phlegm
disease-symptom-phlegm-popup = Mucus builds up in your throat.
disease-symptom-salivation = salivation
disease-symptom-salivation-popup = Your mouth fills with saliva.
disease-symptom-twitch = twitching
disease-symptom-laughter = laughing fits
disease-symptom-screaming = screaming fits
disease-symptom-honking = honking syndrome
disease-symptom-hair-loss = hair loss
disease-symptom-hair-loss-onset = Your hair comes out in clumps.
disease-symptom-voracious-appetite = voracious appetite
disease-symptom-voracious-appetite-onset = A ravenous hunger gnaws at you.
disease-symptom-pacifism = apathy
disease-symptom-pacifism-onset = You have no wish to fight anyone.
disease-symptom-gas-synthesis = gas production
disease-symptom-gas-synthesis-popup = There is a distinctly sour smell.
disease-symptom-bee-cough = bee cough
disease-symptom-bee-cough-popup = You cough up a live bee!
disease-symptom-histamine-synthesis = histamine synthesis
disease-symptom-unstable-nucleotide = unstable nucleotide
disease-symptom-narcolepsy = narcolepsy
disease-symptom-narcolepsy-onset = Your eyelids grow heavy as lead.
disease-symptom-blackout = blackouts
disease-symptom-blackout-popup = Everything goes dark!
disease-symptom-seizures = seizures
disease-symptom-cellular-decay = cellular decay
disease-symptom-radiation-sickness = radiation sickness
disease-symptom-monkism = simian syndrome
disease-symptom-monkism-onset = You crave bananas, and words come harder and harder.

## Syndromes

disease-syndrome-digestive = digestive syndrome
disease-syndrome-febrile = febrile syndrome
disease-syndrome-dermal = dermal syndrome
disease-syndrome-hemorrhagic = hemorrhagic syndrome
disease-syndrome-parasitic = parasitic syndrome
disease-syndrome-anomalous = unclassified syndrome
disease-syndrome-neurological = neurological syndrome

## Report hints

disease-hint-rest = bed rest and warm fluids
disease-hint-fluids = plenty of fluids, watch for dehydration
disease-hint-source = find and destroy the source
disease-hint-mask = sterile masks for the whole department
disease-hint-isolation = isolate the sick immediately
disease-hint-no-suppressants = suppressants mask the symptoms but do not stop the disease, a vaccine is needed
## Diseases

disease-space-cold-name = space cold
disease-space-cold-description = A harmless virus that makes the rounds year-long. Clears up on its own.
disease-space-cold-onset = You feel like you have caught a cold.
disease-mold-allergy-name = spore allergy
disease-mold-allergy-description = A reaction to mold from dark corners. Does not pass between people.
disease-gastroenteritis-name = gastroenteritis
disease-gastroenteritis-description = A gut infection. Spreads through food, dirty hands and shared dishes.
disease-influenza-name = station influenza
disease-influenza-description = A seasonal virus. Takes a department in a couple of hours without masks.
disease-parasites-name = intestinal parasites
disease-parasites-description = Larvae from undercooked meat. Quietly drain strength and blood.
disease-pneumonia-name = pneumonia
disease-pneumonia-description = Bacteria in the lungs. Untreated, it ends in suffocation.
disease-xeno-spores-name = xenosporosis
disease-xeno-spores-description = Alien spores root in skin and lungs. Long incubation, grim outcome.
disease-hemorrhagic-name = hemorrhagic fever
disease-hemorrhagic-description = The virus destroys blood vessels. The patient bleeds from the inside.
disease-anomalous-blight-name = anomalous blight
disease-anomalous-blight-description = An infection of unknown nature. Ordinary suppressants do nothing to it.
disease-roleplay-cold-name = mild cold
disease-roleplay-cold-description = A trifling illness. Not contagious, clears up on its own.
disease-roleplay-cold-onset = Your nose is stuffy and your throat is scratchy.
disease-clown-fever-name = clown fever
disease-clown-fever-description = A harmless but noisy virus: the patient honks, laughs and twitches. Clears up on its own.
disease-clown-fever-onset = Your nose tickles and you feel the urge to honk.
disease-mucus-bronchitis-name = mucus bronchitis
disease-mucus-bronchitis-description = Bacteria in the airways. Phlegm and coughing, spreads through the air.
disease-bacterial-fermentation-name = fermentative dyspepsia
disease-bacterial-fermentation-description = Bacteria from spoiled food ferment in the gut. Brings hunger and gas.
disease-hive-fever-name = hive fever
disease-hive-fever-description = Larvae in the lungs. Itching at first, then the patient coughs up bees.
disease-apathy-virus-name = apathy virus
disease-apathy-virus-description = Saps the will: the patient cannot fight and drifts off to sleep.
disease-neurotropic-fever-name = neurotropic fever
disease-neurotropic-fever-description = The virus attacks the nervous system. Seizures, blindness and sudden sleep.
disease-radian-syndrome-name = Radian's syndrome
disease-radian-syndrome-description = Irradiated tissue breaks down and hair falls out. Spread by hands and tools.
disease-simian-retrovirus-name = simian retrovirus
disease-simian-retrovirus-description = Rewrites the host's genome. In the worst case turns the host into a monkey.

## Объявления

station-event-disease-outbreak-announcement = Attention: an infectious disease outbreak has been detected. Crew are advised to report to medical at the first symptoms.
station-event-disease-vessel-breach-announcement = Attention: a culture vessel in the virology lab has lost containment. Crew are advised to wear masks, avoid the area and report to medical at the first symptoms.
disease-zombie-name = necrotic strain
disease-zombie-description = The agent behind the turning. A vaccine saves the bitten, but brings no one back.
disease-hint-zombie = vaccinate before the turn, isolate those who turned
disease-designed-name = designed strain
disease-designed-description = A strain assembled by hand in virology: its symptom set is exactly what was isolated under a microscope.

# Disease symptom cards in the guidebook. The page is built from symptom prototypes.
