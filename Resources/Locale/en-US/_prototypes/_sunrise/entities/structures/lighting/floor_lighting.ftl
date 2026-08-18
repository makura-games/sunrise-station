ent-AlwaysPoweredFloorLight = floor light
    .desc = { ent-AlwaysPoweredWallLight.desc }
    .suffix = { ent-AlwaysPoweredWallLight.suffix }
ent-FloorLightEmpty = { ent-AlwaysPoweredFloorLight }
    .desc = A floor-mounted lighting fixture. Draws power and produces light when equipped with a light bulb.
    .suffix = { ent-PoweredlightEmpty.suffix }
ent-PoweredFloorlight = { ent-AlwaysPoweredFloorLight }
    .desc = { ent-FloorLightEmpty.desc }

ent-PoweredFloorlightAlwaysPowered = { ent-AlwaysPoweredFloorLight }
    .desc = { ent-FloorLightEmpty.desc }

ent-JapaneseLantern = japanese lantern
    .desc = An elegant stone lantern.
    .suffix = { ent-AlwaysPoweredWallLight.suffix }
ent-JapaneseLanternSmall   = small japanese lantern
    .desc = { ent-JapaneseLantern.desc }
    .suffix = { ent-AlwaysPoweredWallLight.suffix }
ent-PoweredJapaneseLantern = { ent-JapaneseLantern }
    .desc = { ent-JapaneseLantern.desc }
ent-PoweredJapaneseLanternSmall  = { ent-JapaneseLanternSmall }
    .desc = { ent-JapaneseLantern.desc }
