extends Resource
class_name ThingMaterialData

# A material thing does not have a defined structure found in a scene. Instead
# it possess properties that are inherited by singleton things and thus complex
# things as well

# Contains the ratios of each material making up the thing
@export var materials : Dictionary[float, ChangeMaterialData] = {} # Sum of keys = 1.0
# Calculated using materials, if no materials then it should be pre defined
@export var standardDensity : float = 1.0 # g/cm^3 (1.0 is water)
# Used to determine HP essentially
@export var durability : float = 1.0
