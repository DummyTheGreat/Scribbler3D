extends ThingMaterialData
class_name ChangeMaterialData

# ChangeMaterials, they can be combined into new materials. If not a change material
# then they must be broken down into their own materials for creating changes
# Ex: Human arm is not a change material, but blood, tissue, skin, and bones are

enum State {
	SOLID, LIQUID, GAS, PLASMA
}

@export var state : State = State.SOLID
@export var melthingPoint : float = 0.0
@export var boilingPoint : float = 100.0
