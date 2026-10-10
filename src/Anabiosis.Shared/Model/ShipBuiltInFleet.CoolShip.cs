namespace Anabiosis.Shared.Model;

// Raw JSON captured verbatim from the local ship save "крутой корабль" (direct user request: the one hull every enemy is built from) - frozen here
// for the same reason as the player ships in ShipBuiltInFleet.cs, so every install has it. It is NOT in
// ShipBuiltInFleet.Names: it is not a ship the player picks, it is the enemy (EnemyShipLayout.cs).
public static partial class ShipBuiltInFleet
{
    private const string CoolShipJson = """
{
  "Name": "\u0420\u0435\u0430\u043A\u0442\u043E\u0440\u043D\u044B\u0439 \u043E\u0442\u0441\u0435\u043A",
  "Rooms": [
    {
      "Id": "compartment-0",
      "Name": "\u0420\u0435\u0430\u043A\u0442\u043E\u0440\u043D\u044B\u0439 \u043E\u0442\u0441\u0435\u043A \u043D\u043E\u0432\u044B\u0439",
      "Rects": [
        {
          "X": 11,
          "Y": 2,
          "Width": 8,
          "Height": 8,
          "Left": 11,
          "Right": 19,
          "Top": 2,
          "Bottom": 10,
          "Area": 64,
          "Center": {
            "X": 15,
            "Y": 6
          }
        }
      ],
      "X": 11,
      "Y": 2,
      "Width": 8,
      "Height": 8
    },
    {
      "Id": "compartment-1",
      "Name": "\u0429\u0438\u0442\u043E\u043A \u043D\u043E\u0432\u044B\u0439",
      "Rects": [
        {
          "X": 11,
          "Y": -6,
          "Width": 8,
          "Height": 8,
          "Left": 11,
          "Right": 19,
          "Top": -6,
          "Bottom": 2,
          "Area": 64,
          "Center": {
            "X": 15,
            "Y": -2
          }
        }
      ],
      "X": 11,
      "Y": -6,
      "Width": 8,
      "Height": 8
    },
    {
      "Id": "compartment-2",
      "Name": "\u0418\u043D\u0436\u0438\u043D\u0435\u0440\u043D\u044B\u0439 \u043E\u0442\u0441\u0435\u043A \u043D\u043E\u0432\u044B\u0439",
      "Rects": [
        {
          "X": 19,
          "Y": 2,
          "Width": 8,
          "Height": 8,
          "Left": 19,
          "Right": 27,
          "Top": 2,
          "Bottom": 10,
          "Area": 64,
          "Center": {
            "X": 23,
            "Y": 6
          }
        }
      ],
      "X": 19,
      "Y": 2,
      "Width": 8,
      "Height": 8
    },
    {
      "Id": "compartment-3",
      "Name": "\u0418\u043D\u0436\u0438\u043D\u0435\u0440\u043D\u044B\u0439 \u043E\u0442\u0441\u0435\u043A \u043D\u043E\u0432\u044B\u0439",
      "Rects": [
        {
          "X": 3,
          "Y": 2,
          "Width": 8,
          "Height": 8,
          "Left": 3,
          "Right": 11,
          "Top": 2,
          "Bottom": 10,
          "Area": 64,
          "Center": {
            "X": 7,
            "Y": 6
          }
        }
      ],
      "X": 3,
      "Y": 2,
      "Width": 8,
      "Height": 8
    },
    {
      "Id": "compartment-36",
      "Name": "\u041D\u043E\u0432\u044B\u0439 \u043A\u043E\u043A\u043F\u0438\u0442",
      "Rects": [
        {
          "X": 11,
          "Y": -14,
          "Width": 8,
          "Height": 8,
          "Left": 11,
          "Right": 19,
          "Top": -14,
          "Bottom": -6,
          "Area": 64,
          "Center": {
            "X": 15,
            "Y": -10
          }
        }
      ],
      "X": 11,
      "Y": -14,
      "Width": 8,
      "Height": 8
    },
    {
      "Id": "compartment-6",
      "Name": "\u0422\u0443\u0440\u0435\u043B\u044C \u043D\u043E\u0432\u0430\u044F",
      "Rects": [
        {
          "X": 19,
          "Y": -2,
          "Width": 4,
          "Height": 4,
          "Left": 19,
          "Right": 23,
          "Top": -2,
          "Bottom": 2,
          "Area": 16,
          "Center": {
            "X": 21,
            "Y": 0
          }
        }
      ],
      "X": 19,
      "Y": -2,
      "Width": 4,
      "Height": 4
    },
    {
      "Id": "compartment-7",
      "Name": "\u0422\u0443\u0440\u0435\u043B\u044C \u043D\u043E\u0432\u0430\u044F",
      "Rects": [
        {
          "X": 7,
          "Y": -2,
          "Width": 4,
          "Height": 4,
          "Left": 7,
          "Right": 11,
          "Top": -2,
          "Bottom": 2,
          "Area": 16,
          "Center": {
            "X": 9,
            "Y": 0
          }
        }
      ],
      "X": 7,
      "Y": -2,
      "Width": 4,
      "Height": 4
    },
    {
      "Id": "compartment-45",
      "Name": "\u0414\u0432\u0438\u0433\u0430\u0442\u0435\u043B\u044C \u043D\u043E\u0432\u044B\u0439 (1)",
      "Rects": [
        {
          "X": 7,
          "Y": -6,
          "Width": 4,
          "Height": 4,
          "Left": 7,
          "Right": 11,
          "Top": -6,
          "Bottom": -2,
          "Area": 16,
          "Center": {
            "X": 9,
            "Y": -4
          }
        }
      ],
      "X": 7,
      "Y": -6,
      "Width": 4,
      "Height": 4
    },
    {
      "Id": "compartment-50",
      "Name": "\u0414\u0432\u0438\u0433\u0430\u0442\u0435\u043B\u044C \u043D\u043E\u0432\u044B\u0439 (1)",
      "Rects": [
        {
          "X": 19,
          "Y": -6,
          "Width": 4,
          "Height": 4,
          "Left": 19,
          "Right": 23,
          "Top": -6,
          "Bottom": -2,
          "Area": 16,
          "Center": {
            "X": 21,
            "Y": -4
          }
        }
      ],
      "X": 19,
      "Y": -6,
      "Width": 4,
      "Height": 4
    },
    {
      "Id": "compartment-42",
      "Name": "\u0414\u0432\u0438\u0433\u0430\u0442\u0435\u043B\u044C \u043D\u043E\u0432\u044B\u0439 (1)",
      "Rects": [
        {
          "X": 15,
          "Y": -18,
          "Width": 4,
          "Height": 4,
          "Left": 15,
          "Right": 19,
          "Top": -18,
          "Bottom": -14,
          "Area": 16,
          "Center": {
            "X": 17,
            "Y": -16
          }
        }
      ],
      "X": 15,
      "Y": -18,
      "Width": 4,
      "Height": 4
    },
    {
      "Id": "compartment-43",
      "Name": "\u0414\u0432\u0438\u0433\u0430\u0442\u0435\u043B\u044C \u043D\u043E\u0432\u044B\u0439 (1)",
      "Rects": [
        {
          "X": 11,
          "Y": -18,
          "Width": 4,
          "Height": 4,
          "Left": 11,
          "Right": 15,
          "Top": -18,
          "Bottom": -14,
          "Area": 16,
          "Center": {
            "X": 13,
            "Y": -16
          }
        }
      ],
      "X": 11,
      "Y": -18,
      "Width": 4,
      "Height": 4
    },
    {
      "Id": "compartment-52",
      "Name": "\u0428\u043B\u044E\u0437 \u043D\u043E\u0432\u044B\u0439",
      "Rects": [
        {
          "X": 27,
          "Y": 2,
          "Width": 4,
          "Height": 4,
          "Left": 27,
          "Right": 31,
          "Top": 2,
          "Bottom": 6,
          "Area": 16,
          "Center": {
            "X": 29,
            "Y": 4
          }
        }
      ],
      "X": 27,
      "Y": 2,
      "Width": 4,
      "Height": 4
    },
    {
      "Id": "compartment-25",
      "Name": "\u0414\u0432\u0438\u0433\u0430\u0442\u0435\u043B\u044C \u043D\u043E\u0432\u044B\u0439 (1)",
      "Rects": [
        {
          "X": -1,
          "Y": 2,
          "Width": 4,
          "Height": 4,
          "Left": -1,
          "Right": 3,
          "Top": 2,
          "Bottom": 6,
          "Area": 16,
          "Center": {
            "X": 1,
            "Y": 4
          }
        }
      ],
      "X": -1,
      "Y": 2,
      "Width": 4,
      "Height": 4
    },
    {
      "Id": "compartment-30",
      "Name": "\u0414\u0432\u0438\u0433\u0430\u0442\u0435\u043B\u044C \u043D\u043E\u0432\u044B\u0439 (1)",
      "Rects": [
        {
          "X": 19,
          "Y": 10,
          "Width": 4,
          "Height": 4,
          "Left": 19,
          "Right": 23,
          "Top": 10,
          "Bottom": 14,
          "Area": 16,
          "Center": {
            "X": 21,
            "Y": 12
          }
        }
      ],
      "X": 19,
      "Y": 10,
      "Width": 4,
      "Height": 4
    },
    {
      "Id": "compartment-31",
      "Name": "\u0414\u0432\u0438\u0433\u0430\u0442\u0435\u043B\u044C \u043D\u043E\u0432\u044B\u0439 (1)",
      "Rects": [
        {
          "X": 23,
          "Y": 10,
          "Width": 4,
          "Height": 4,
          "Left": 23,
          "Right": 27,
          "Top": 10,
          "Bottom": 14,
          "Area": 16,
          "Center": {
            "X": 25,
            "Y": 12
          }
        }
      ],
      "X": 23,
      "Y": 10,
      "Width": 4,
      "Height": 4
    },
    {
      "Id": "compartment-32",
      "Name": "\u0414\u0432\u0438\u0433\u0430\u0442\u0435\u043B\u044C \u043D\u043E\u0432\u044B\u0439 (1)",
      "Rects": [
        {
          "X": 7,
          "Y": 10,
          "Width": 4,
          "Height": 4,
          "Left": 7,
          "Right": 11,
          "Top": 10,
          "Bottom": 14,
          "Area": 16,
          "Center": {
            "X": 9,
            "Y": 12
          }
        }
      ],
      "X": 7,
      "Y": 10,
      "Width": 4,
      "Height": 4
    },
    {
      "Id": "compartment-33",
      "Name": "\u0414\u0432\u0438\u0433\u0430\u0442\u0435\u043B\u044C \u043D\u043E\u0432\u044B\u0439 (1)",
      "Rects": [
        {
          "X": 3,
          "Y": 10,
          "Width": 4,
          "Height": 4,
          "Left": 3,
          "Right": 7,
          "Top": 10,
          "Bottom": 14,
          "Area": 16,
          "Center": {
            "X": 5,
            "Y": 12
          }
        }
      ],
      "X": 3,
      "Y": 10,
      "Width": 4,
      "Height": 4
    }
  ],
  "Doors": [],
  "Airlocks": [],
  "Devices": [
    {
      "Kind": "Reactor",
      "X": 15,
      "Y": 7,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "Distribution",
      "X": 15.5,
      "Y": 4.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "FuelRodStorage",
      "X": 14.5,
      "Y": 4.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "Battery",
      "X": 14.5,
      "Y": -3.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "Junction",
      "X": 16,
      "Y": -2.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "HalfWidthSide": "East",
      "Id": null
    },
    {
      "Kind": "Junction",
      "X": 16,
      "Y": -1.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "HalfWidthSide": "East",
      "Id": null
    },
    {
      "Kind": "Battery",
      "X": 15.5,
      "Y": -3.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "Junction",
      "X": 14,
      "Y": -1.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "HalfWidthSide": "West",
      "Id": null
    },
    {
      "Kind": "Junction",
      "X": 14,
      "Y": -2.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "HalfWidthSide": "West",
      "Id": null
    },
    {
      "Kind": "Junction",
      "X": 14,
      "Y": -0.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "HalfWidthSide": "West",
      "Id": null
    },
    {
      "Kind": "Junction",
      "X": 16,
      "Y": -0.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "HalfWidthSide": "East",
      "Id": null
    },
    {
      "Kind": "Deconstructor",
      "X": 21.5,
      "Y": 3,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "HalfWidthSide": "North",
      "Id": null
    },
    {
      "Kind": "Fabricator",
      "X": 24.5,
      "Y": 3,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "HalfWidthSide": "North",
      "Id": null
    },
    {
      "Kind": "WeaponWorkbench",
      "X": 23,
      "Y": 7,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "Deconstructor",
      "X": 5.5,
      "Y": 3,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "HalfWidthSide": "North",
      "Id": null
    },
    {
      "Kind": "Fabricator",
      "X": 8.5,
      "Y": 3,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "HalfWidthSide": "North",
      "Id": null
    },
    {
      "Kind": "WeaponWorkbench",
      "X": 7,
      "Y": 7,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "Helm",
      "X": 18,
      "Y": -9,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "HalfWidthSide": "East",
      "Id": null
    },
    {
      "Kind": "Navigation",
      "X": 18,
      "Y": -11,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "HalfWidthSide": "East",
      "Id": null
    },
    {
      "Kind": "CommsConsole",
      "X": 12,
      "Y": -9,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "HalfWidthSide": "West",
      "Id": null
    },
    {
      "Kind": "ShipStatusMonitor",
      "X": 12,
      "Y": -11,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "HalfWidthSide": "West",
      "Id": null
    },
    {
      "Kind": "TurretBallistic",
      "X": 21.5,
      "Y": 0,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "TurretBallistic",
      "X": 8.5,
      "Y": 0,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "Oxygen",
      "X": 12.5,
      "Y": 3.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "SuitLocker",
      "X": 13.5,
      "Y": 3.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "StorageRack",
      "X": 14.5,
      "Y": 3.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "StorageRack",
      "X": 15.5,
      "Y": 3.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "StorageRack",
      "X": 16.5,
      "Y": 3.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 12.5,
      "Y": 9.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "North",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 17.5,
      "Y": 9.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "North",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 12.5,
      "Y": -5.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "South",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 12.5,
      "Y": 1.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "North",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 17.5,
      "Y": -5.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "South",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 17.5,
      "Y": 1.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "North",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 19.5,
      "Y": 3.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "East",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 20.5,
      "Y": 9.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "North",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 25.5,
      "Y": 9.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "North",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 26.5,
      "Y": 3.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "West",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 3.5,
      "Y": 3.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "East",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 4.5,
      "Y": 9.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "North",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 9.5,
      "Y": 9.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "North",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 10.5,
      "Y": 3.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "West",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 18.5,
      "Y": -7.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "West",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 18.5,
      "Y": -12.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "West",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "Terminal",
      "X": 14.5,
      "Y": -8.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "West",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "Terminal",
      "X": 14.5,
      "Y": -9.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "West",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "Terminal",
      "X": 14.5,
      "Y": -10.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "West",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "Terminal",
      "X": 14.5,
      "Y": -11.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "West",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 11.5,
      "Y": -7.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "East",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 11.5,
      "Y": -12.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "East",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 9.5,
      "Y": -2.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "North",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 9.5,
      "Y": -5.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "South",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 20.5,
      "Y": -2.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "North",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 20.5,
      "Y": -5.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "South",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 15.5,
      "Y": -15.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "East",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 18.5,
      "Y": -15.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "West",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 11.5,
      "Y": -15.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "East",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 14.5,
      "Y": -15.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "West",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 1.5,
      "Y": 2.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "South",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 1.5,
      "Y": 5.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "North",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 19.5,
      "Y": 11.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "East",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 22.5,
      "Y": 11.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "West",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 23.5,
      "Y": 11.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "East",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 26.5,
      "Y": 11.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "West",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 7.5,
      "Y": 11.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "East",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 10.5,
      "Y": 11.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "West",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 3.5,
      "Y": 11.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "East",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    },
    {
      "Kind": "WallLamp",
      "X": 6.5,
      "Y": 11.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": "West",
      "Rotated": false,
      "HalfWidthSide": null,
      "Id": null
    }
  ],
  "ForwardDegrees": 0,
  "WallMaterialsRaw": [],
  "EnginesRaw": [
    {
      "X": 8.5,
      "Y": -4.5,
      "Facing": "West",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 8.5,
      "Y": -3.5,
      "Facing": "West",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 21.5,
      "Y": -3.5,
      "Facing": "East",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 21.5,
      "Y": -4.5,
      "Facing": "East",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 17.5,
      "Y": -16.5,
      "Facing": "North",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 16.5,
      "Y": -16.5,
      "Facing": "North",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 13.5,
      "Y": -16.5,
      "Facing": "North",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 12.5,
      "Y": -16.5,
      "Facing": "North",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 0.5,
      "Y": 3.5,
      "Facing": "West",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 0.5,
      "Y": 4.5,
      "Facing": "West",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 21.5,
      "Y": 12.5,
      "Facing": "South",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 20.5,
      "Y": 12.5,
      "Facing": "South",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 24.5,
      "Y": 12.5,
      "Facing": "South",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 25.5,
      "Y": 12.5,
      "Facing": "South",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 8.5,
      "Y": 12.5,
      "Facing": "South",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 9.5,
      "Y": 12.5,
      "Facing": "South",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 4.5,
      "Y": 12.5,
      "Facing": "South",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 5.5,
      "Y": 12.5,
      "Facing": "South",
      "MaxThrust": 8,
      "Role": "Marching"
    }
  ],
  "SupplementalWallTilesRaw": [
    {
      "X": 13,
      "Y": 4
    },
    {
      "X": 16,
      "Y": 4
    },
    {
      "X": 18,
      "Y": 3
    },
    {
      "X": 18,
      "Y": 4
    },
    {
      "X": 18,
      "Y": 7
    },
    {
      "X": 18,
      "Y": 8
    },
    {
      "X": 12,
      "Y": 1
    },
    {
      "X": 13,
      "Y": 1
    },
    {
      "X": 16,
      "Y": 1
    },
    {
      "X": 17,
      "Y": 1
    },
    {
      "X": 18,
      "Y": -4
    },
    {
      "X": 18,
      "Y": -3
    },
    {
      "X": 18,
      "Y": -2
    },
    {
      "X": 18,
      "Y": 0
    },
    {
      "X": 18,
      "Y": 1
    },
    {
      "X": 20,
      "Y": 9
    },
    {
      "X": 21,
      "Y": 5
    },
    {
      "X": 22,
      "Y": 5
    },
    {
      "X": 22,
      "Y": 9
    },
    {
      "X": 23,
      "Y": 5
    },
    {
      "X": 23,
      "Y": 9
    },
    {
      "X": 24,
      "Y": 5
    },
    {
      "X": 25,
      "Y": 9
    },
    {
      "X": 26,
      "Y": 3
    },
    {
      "X": 26,
      "Y": 5
    },
    {
      "X": 4,
      "Y": 9
    },
    {
      "X": 5,
      "Y": 5
    },
    {
      "X": 6,
      "Y": 5
    },
    {
      "X": 6,
      "Y": 9
    },
    {
      "X": 7,
      "Y": 5
    },
    {
      "X": 7,
      "Y": 9
    },
    {
      "X": 8,
      "Y": 5
    },
    {
      "X": 9,
      "Y": 9
    },
    {
      "X": 10,
      "Y": 3
    },
    {
      "X": 10,
      "Y": 4
    },
    {
      "X": 10,
      "Y": 7
    },
    {
      "X": 10,
      "Y": 8
    },
    {
      "X": 10,
      "Y": 9
    },
    {
      "X": 17,
      "Y": -7
    },
    {
      "X": 16,
      "Y": -7
    },
    {
      "X": 14,
      "Y": -9
    },
    {
      "X": 14,
      "Y": -10
    },
    {
      "X": 14,
      "Y": -11
    },
    {
      "X": 14,
      "Y": -12
    },
    {
      "X": 13,
      "Y": -7
    },
    {
      "X": 12,
      "Y": -7
    },
    {
      "X": 21,
      "Y": 1
    },
    {
      "X": 20,
      "Y": 1
    },
    {
      "X": 8,
      "Y": 1
    },
    {
      "X": 9,
      "Y": 1
    },
    {
      "X": 10,
      "Y": 0
    },
    {
      "X": 10,
      "Y": 1
    },
    {
      "X": 10,
      "Y": -3
    },
    {
      "X": 10,
      "Y": -4
    },
    {
      "X": 9,
      "Y": -3
    },
    {
      "X": 8,
      "Y": -3
    },
    {
      "X": 21,
      "Y": -3
    },
    {
      "X": 20,
      "Y": -3
    },
    {
      "X": 14,
      "Y": -17
    },
    {
      "X": 14,
      "Y": -16
    },
    {
      "X": 14,
      "Y": -15
    },
    {
      "X": 2,
      "Y": 3
    },
    {
      "X": 22,
      "Y": 11
    },
    {
      "X": 22,
      "Y": 12
    },
    {
      "X": 6,
      "Y": 11
    },
    {
      "X": 6,
      "Y": 12
    }
  ],
  "ForcedFloorTilesRaw": [
    {
      "X": 15,
      "Y": -7
    },
    {
      "X": 15,
      "Y": -8
    },
    {
      "X": 14,
      "Y": -7
    },
    {
      "X": 15,
      "Y": -9
    },
    {
      "X": 16,
      "Y": -8
    },
    {
      "X": 14,
      "Y": -8
    },
    {
      "X": 15,
      "Y": -10
    },
    {
      "X": 16,
      "Y": -9
    },
    {
      "X": 17,
      "Y": -8
    },
    {
      "X": 13,
      "Y": -8
    },
    {
      "X": 15,
      "Y": -11
    },
    {
      "X": 16,
      "Y": -10
    },
    {
      "X": 17,
      "Y": -9
    },
    {
      "X": 13,
      "Y": -9
    },
    {
      "X": 12,
      "Y": -8
    },
    {
      "X": 15,
      "Y": -12
    },
    {
      "X": 16,
      "Y": -11
    },
    {
      "X": 17,
      "Y": -10
    },
    {
      "X": 13,
      "Y": -10
    },
    {
      "X": 12,
      "Y": -9
    },
    {
      "X": 15,
      "Y": -13
    },
    {
      "X": 16,
      "Y": -12
    },
    {
      "X": 17,
      "Y": -11
    },
    {
      "X": 13,
      "Y": -11
    },
    {
      "X": 12,
      "Y": -10
    },
    {
      "X": 16,
      "Y": -13
    },
    {
      "X": 14,
      "Y": -13
    },
    {
      "X": 17,
      "Y": -12
    },
    {
      "X": 13,
      "Y": -12
    },
    {
      "X": 12,
      "Y": -11
    },
    {
      "X": 16,
      "Y": -14
    },
    {
      "X": 17,
      "Y": -13
    },
    {
      "X": 13,
      "Y": -13
    },
    {
      "X": 12,
      "Y": -12
    },
    {
      "X": 17,
      "Y": -14
    },
    {
      "X": 13,
      "Y": -14
    },
    {
      "X": 12,
      "Y": -13
    },
    {
      "X": 12,
      "Y": -14
    },
    {
      "X": 28,
      "Y": 4
    },
    {
      "X": 28,
      "Y": 3
    },
    {
      "X": 29,
      "Y": 4
    },
    {
      "X": 27,
      "Y": 4
    },
    {
      "X": 29,
      "Y": 3
    },
    {
      "X": 16,
      "Y": -15
    },
    {
      "X": 16,
      "Y": -16
    },
    {
      "X": 17,
      "Y": -15
    },
    {
      "X": 16,
      "Y": -17
    },
    {
      "X": 17,
      "Y": -16
    },
    {
      "X": 17,
      "Y": -17
    },
    {
      "X": 13,
      "Y": -15
    },
    {
      "X": 13,
      "Y": -16
    },
    {
      "X": 12,
      "Y": -15
    },
    {
      "X": 13,
      "Y": -17
    },
    {
      "X": 12,
      "Y": -16
    },
    {
      "X": 12,
      "Y": -17
    },
    {
      "X": 15,
      "Y": 1
    },
    {
      "X": 15,
      "Y": 0
    },
    {
      "X": 14,
      "Y": 1
    },
    {
      "X": 15,
      "Y": -1
    },
    {
      "X": 16,
      "Y": 0
    },
    {
      "X": 14,
      "Y": 0
    },
    {
      "X": 15,
      "Y": -2
    },
    {
      "X": 16,
      "Y": -1
    },
    {
      "X": 14,
      "Y": -1
    },
    {
      "X": 17,
      "Y": 0
    },
    {
      "X": 13,
      "Y": 0
    },
    {
      "X": 15,
      "Y": -3
    },
    {
      "X": 16,
      "Y": -2
    },
    {
      "X": 14,
      "Y": -2
    },
    {
      "X": 17,
      "Y": -1
    },
    {
      "X": 13,
      "Y": -1
    },
    {
      "X": 12,
      "Y": 0
    },
    {
      "X": 15,
      "Y": -4
    },
    {
      "X": 16,
      "Y": -3
    },
    {
      "X": 14,
      "Y": -3
    },
    {
      "X": 17,
      "Y": -2
    },
    {
      "X": 13,
      "Y": -2
    },
    {
      "X": 18,
      "Y": -1
    },
    {
      "X": 12,
      "Y": -1
    },
    {
      "X": 15,
      "Y": -5
    },
    {
      "X": 16,
      "Y": -4
    },
    {
      "X": 14,
      "Y": -4
    },
    {
      "X": 17,
      "Y": -3
    },
    {
      "X": 13,
      "Y": -3
    },
    {
      "X": 12,
      "Y": -2
    },
    {
      "X": 11,
      "Y": -1
    },
    {
      "X": 15,
      "Y": -6
    },
    {
      "X": 16,
      "Y": -5
    },
    {
      "X": 14,
      "Y": -5
    },
    {
      "X": 17,
      "Y": -4
    },
    {
      "X": 13,
      "Y": -4
    },
    {
      "X": 12,
      "Y": -3
    },
    {
      "X": 14,
      "Y": -6
    },
    {
      "X": 17,
      "Y": -5
    },
    {
      "X": 13,
      "Y": -5
    },
    {
      "X": 12,
      "Y": -4
    },
    {
      "X": 18,
      "Y": -5
    },
    {
      "X": 12,
      "Y": -5
    },
    {
      "X": 11,
      "Y": -5
    },
    {
      "X": 18,
      "Y": 5
    },
    {
      "X": 18,
      "Y": 6
    },
    {
      "X": 17,
      "Y": 5
    },
    {
      "X": 17,
      "Y": 6
    },
    {
      "X": 17,
      "Y": 4
    },
    {
      "X": 16,
      "Y": 5
    },
    {
      "X": 17,
      "Y": 7
    },
    {
      "X": 16,
      "Y": 6
    },
    {
      "X": 17,
      "Y": 3
    },
    {
      "X": 15,
      "Y": 5
    },
    {
      "X": 17,
      "Y": 8
    },
    {
      "X": 16,
      "Y": 7
    },
    {
      "X": 15,
      "Y": 6
    },
    {
      "X": 16,
      "Y": 3
    },
    {
      "X": 15,
      "Y": 4
    },
    {
      "X": 14,
      "Y": 5
    },
    {
      "X": 16,
      "Y": 8
    },
    {
      "X": 15,
      "Y": 7
    },
    {
      "X": 14,
      "Y": 6
    },
    {
      "X": 15,
      "Y": 3
    },
    {
      "X": 14,
      "Y": 4
    },
    {
      "X": 13,
      "Y": 5
    },
    {
      "X": 15,
      "Y": 8
    },
    {
      "X": 14,
      "Y": 7
    },
    {
      "X": 13,
      "Y": 6
    },
    {
      "X": 15,
      "Y": 2
    },
    {
      "X": 14,
      "Y": 3
    },
    {
      "X": 12,
      "Y": 5
    },
    {
      "X": 14,
      "Y": 8
    },
    {
      "X": 13,
      "Y": 7
    },
    {
      "X": 12,
      "Y": 6
    },
    {
      "X": 14,
      "Y": 2
    },
    {
      "X": 13,
      "Y": 3
    },
    {
      "X": 12,
      "Y": 4
    },
    {
      "X": 11,
      "Y": 5
    },
    {
      "X": 13,
      "Y": 8
    },
    {
      "X": 12,
      "Y": 7
    },
    {
      "X": 11,
      "Y": 6
    },
    {
      "X": 12,
      "Y": 3
    },
    {
      "X": 12,
      "Y": 8
    },
    {
      "X": 10,
      "Y": 5
    },
    {
      "X": 10,
      "Y": 6
    },
    {
      "X": 9,
      "Y": 5
    },
    {
      "X": 9,
      "Y": 6
    },
    {
      "X": 9,
      "Y": 4
    },
    {
      "X": 9,
      "Y": 7
    },
    {
      "X": 8,
      "Y": 6
    },
    {
      "X": 9,
      "Y": 3
    },
    {
      "X": 8,
      "Y": 4
    },
    {
      "X": 9,
      "Y": 8
    },
    {
      "X": 8,
      "Y": 7
    },
    {
      "X": 7,
      "Y": 6
    },
    {
      "X": 8,
      "Y": 3
    },
    {
      "X": 7,
      "Y": 4
    },
    {
      "X": 8,
      "Y": 8
    },
    {
      "X": 7,
      "Y": 7
    },
    {
      "X": 6,
      "Y": 6
    },
    {
      "X": 7,
      "Y": 3
    },
    {
      "X": 6,
      "Y": 4
    },
    {
      "X": 8,
      "Y": 9
    },
    {
      "X": 7,
      "Y": 8
    },
    {
      "X": 6,
      "Y": 7
    },
    {
      "X": 5,
      "Y": 6
    },
    {
      "X": 6,
      "Y": 3
    },
    {
      "X": 5,
      "Y": 4
    },
    {
      "X": 6,
      "Y": 8
    },
    {
      "X": 5,
      "Y": 7
    },
    {
      "X": 4,
      "Y": 6
    },
    {
      "X": 5,
      "Y": 3
    },
    {
      "X": 4,
      "Y": 4
    },
    {
      "X": 5,
      "Y": 8
    },
    {
      "X": 4,
      "Y": 7
    },
    {
      "X": 4,
      "Y": 5
    },
    {
      "X": 4,
      "Y": 3
    },
    {
      "X": 3,
      "Y": 4
    },
    {
      "X": 5,
      "Y": 9
    },
    {
      "X": 4,
      "Y": 8
    },
    {
      "X": 19,
      "Y": -5
    },
    {
      "X": 20,
      "Y": -5
    },
    {
      "X": 20,
      "Y": -4
    },
    {
      "X": 21,
      "Y": -5
    },
    {
      "X": 21,
      "Y": -4
    },
    {
      "X": 10,
      "Y": -5
    },
    {
      "X": 9,
      "Y": -5
    },
    {
      "X": 9,
      "Y": -4
    },
    {
      "X": 8,
      "Y": -5
    },
    {
      "X": 8,
      "Y": -4
    },
    {
      "X": 10,
      "Y": -1
    },
    {
      "X": 9,
      "Y": -1
    },
    {
      "X": 9,
      "Y": 0
    },
    {
      "X": 8,
      "Y": -1
    },
    {
      "X": 8,
      "Y": 0
    },
    {
      "X": 19,
      "Y": -1
    },
    {
      "X": 20,
      "Y": -1
    },
    {
      "X": 20,
      "Y": 0
    },
    {
      "X": 21,
      "Y": -1
    },
    {
      "X": 21,
      "Y": 0
    },
    {
      "X": 24,
      "Y": 10
    },
    {
      "X": 24,
      "Y": 11
    },
    {
      "X": 24,
      "Y": 12
    },
    {
      "X": 25,
      "Y": 11
    },
    {
      "X": 25,
      "Y": 12
    },
    {
      "X": 21,
      "Y": 10
    },
    {
      "X": 21,
      "Y": 11
    },
    {
      "X": 21,
      "Y": 12
    },
    {
      "X": 20,
      "Y": 11
    },
    {
      "X": 20,
      "Y": 12
    },
    {
      "X": 8,
      "Y": 10
    },
    {
      "X": 8,
      "Y": 11
    },
    {
      "X": 8,
      "Y": 12
    },
    {
      "X": 9,
      "Y": 11
    },
    {
      "X": 9,
      "Y": 12
    },
    {
      "X": 5,
      "Y": 10
    },
    {
      "X": 5,
      "Y": 11
    },
    {
      "X": 5,
      "Y": 12
    },
    {
      "X": 4,
      "Y": 11
    },
    {
      "X": 4,
      "Y": 12
    },
    {
      "X": 26,
      "Y": 4
    },
    {
      "X": 25,
      "Y": 4
    },
    {
      "X": 25,
      "Y": 3
    },
    {
      "X": 25,
      "Y": 5
    },
    {
      "X": 24,
      "Y": 4
    },
    {
      "X": 24,
      "Y": 3
    },
    {
      "X": 25,
      "Y": 6
    },
    {
      "X": 23,
      "Y": 4
    },
    {
      "X": 23,
      "Y": 3
    },
    {
      "X": 25,
      "Y": 7
    },
    {
      "X": 24,
      "Y": 6
    },
    {
      "X": 22,
      "Y": 4
    },
    {
      "X": 22,
      "Y": 3
    },
    {
      "X": 25,
      "Y": 8
    },
    {
      "X": 24,
      "Y": 7
    },
    {
      "X": 23,
      "Y": 6
    },
    {
      "X": 21,
      "Y": 4
    },
    {
      "X": 21,
      "Y": 3
    },
    {
      "X": 24,
      "Y": 8
    },
    {
      "X": 23,
      "Y": 7
    },
    {
      "X": 22,
      "Y": 6
    },
    {
      "X": 20,
      "Y": 4
    },
    {
      "X": 20,
      "Y": 3
    },
    {
      "X": 24,
      "Y": 9
    },
    {
      "X": 23,
      "Y": 8
    },
    {
      "X": 22,
      "Y": 7
    },
    {
      "X": 21,
      "Y": 6
    },
    {
      "X": 20,
      "Y": 5
    },
    {
      "X": 22,
      "Y": 8
    },
    {
      "X": 21,
      "Y": 7
    },
    {
      "X": 20,
      "Y": 6
    },
    {
      "X": 19,
      "Y": 5
    },
    {
      "X": 21,
      "Y": 8
    },
    {
      "X": 20,
      "Y": 7
    },
    {
      "X": 19,
      "Y": 6
    },
    {
      "X": 21,
      "Y": 9
    },
    {
      "X": 20,
      "Y": 8
    },
    {
      "X": 2,
      "Y": 4
    },
    {
      "X": 1,
      "Y": 4
    },
    {
      "X": 1,
      "Y": 3
    },
    {
      "X": 0,
      "Y": 4
    },
    {
      "X": 0,
      "Y": 3
    }
  ],
  "WallOpenSidesRaw": [
    {
      "X": 11,
      "Y": 3,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": 4,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": 7,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": 8,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 12,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 13,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 13,
      "Y": 4,
      "Side": "South"
    },
    {
      "X": 13,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 14,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 15,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 16,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 16,
      "Y": 4,
      "Side": "South"
    },
    {
      "X": 16,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 17,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 17,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 18,
      "Y": 3,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": 4,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": 7,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": 8,
      "Side": "East"
    },
    {
      "X": 11,
      "Y": -4,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": -3,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": -2,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": 0,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": -6,
      "Side": "North"
    },
    {
      "X": 12,
      "Y": 1,
      "Side": "South"
    },
    {
      "X": 13,
      "Y": -6,
      "Side": "North"
    },
    {
      "X": 13,
      "Y": 1,
      "Side": "South"
    },
    {
      "X": 16,
      "Y": -6,
      "Side": "North"
    },
    {
      "X": 16,
      "Y": 1,
      "Side": "South"
    },
    {
      "X": 17,
      "Y": -6,
      "Side": "North"
    },
    {
      "X": 17,
      "Y": 1,
      "Side": "South"
    },
    {
      "X": 18,
      "Y": -4,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": -3,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": -2,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": 0,
      "Side": "East"
    },
    {
      "X": 19,
      "Y": 3,
      "Side": "West"
    },
    {
      "X": 19,
      "Y": 4,
      "Side": "West"
    },
    {
      "X": 19,
      "Y": 7,
      "Side": "West"
    },
    {
      "X": 19,
      "Y": 8,
      "Side": "West"
    },
    {
      "X": 20,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 20,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 21,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 21,
      "Y": 5,
      "Side": "South"
    },
    {
      "X": 22,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 22,
      "Y": 5,
      "Side": "South"
    },
    {
      "X": 22,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 23,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 23,
      "Y": 5,
      "Side": "South"
    },
    {
      "X": 23,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 24,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 24,
      "Y": 5,
      "Side": "South"
    },
    {
      "X": 25,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 25,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 26,
      "Y": 3,
      "Side": "East"
    },
    {
      "X": 26,
      "Y": 5,
      "Side": "East"
    },
    {
      "X": 26,
      "Y": 6,
      "Side": "East"
    },
    {
      "X": 26,
      "Y": 7,
      "Side": "East"
    },
    {
      "X": 26,
      "Y": 8,
      "Side": "East"
    },
    {
      "X": 3,
      "Y": 3,
      "Side": "West"
    },
    {
      "X": 3,
      "Y": 5,
      "Side": "West"
    },
    {
      "X": 3,
      "Y": 6,
      "Side": "West"
    },
    {
      "X": 3,
      "Y": 7,
      "Side": "West"
    },
    {
      "X": 3,
      "Y": 8,
      "Side": "West"
    },
    {
      "X": 4,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 4,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 5,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 5,
      "Y": 5,
      "Side": "South"
    },
    {
      "X": 6,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 6,
      "Y": 5,
      "Side": "South"
    },
    {
      "X": 6,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 7,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 7,
      "Y": 5,
      "Side": "South"
    },
    {
      "X": 7,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 8,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 8,
      "Y": 5,
      "Side": "South"
    },
    {
      "X": 9,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 9,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 10,
      "Y": 3,
      "Side": "East"
    },
    {
      "X": 10,
      "Y": 4,
      "Side": "East"
    },
    {
      "X": 10,
      "Y": 7,
      "Side": "East"
    },
    {
      "X": 10,
      "Y": 8,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": -8,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": -9,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": -10,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": -11,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": -12,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": -13,
      "Side": "East"
    },
    {
      "X": 17,
      "Y": -7,
      "Side": "South"
    },
    {
      "X": 16,
      "Y": -7,
      "Side": "South"
    },
    {
      "X": 15,
      "Y": -14,
      "Side": "North"
    },
    {
      "X": 14,
      "Y": -9,
      "Side": "East"
    },
    {
      "X": 14,
      "Y": -10,
      "Side": "East"
    },
    {
      "X": 14,
      "Y": -11,
      "Side": "East"
    },
    {
      "X": 14,
      "Y": -12,
      "Side": "East"
    },
    {
      "X": 14,
      "Y": -14,
      "Side": "North"
    },
    {
      "X": 13,
      "Y": -7,
      "Side": "South"
    },
    {
      "X": 12,
      "Y": -7,
      "Side": "South"
    },
    {
      "X": 11,
      "Y": -8,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": -9,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": -10,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": -11,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": -12,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": -13,
      "Side": "West"
    },
    {
      "X": 22,
      "Y": 0,
      "Side": "East"
    },
    {
      "X": 22,
      "Y": -1,
      "Side": "East"
    },
    {
      "X": 21,
      "Y": 1,
      "Side": "South"
    },
    {
      "X": 21,
      "Y": -2,
      "Side": "North"
    },
    {
      "X": 20,
      "Y": 1,
      "Side": "South"
    },
    {
      "X": 20,
      "Y": -2,
      "Side": "North"
    },
    {
      "X": 19,
      "Y": 0,
      "Side": "West"
    },
    {
      "X": 7,
      "Y": -1,
      "Side": "West"
    },
    {
      "X": 7,
      "Y": 0,
      "Side": "West"
    },
    {
      "X": 8,
      "Y": -2,
      "Side": "North"
    },
    {
      "X": 8,
      "Y": 1,
      "Side": "South"
    },
    {
      "X": 9,
      "Y": -2,
      "Side": "North"
    },
    {
      "X": 9,
      "Y": 1,
      "Side": "South"
    },
    {
      "X": 10,
      "Y": 0,
      "Side": "East"
    },
    {
      "X": 10,
      "Y": -4,
      "Side": "East"
    },
    {
      "X": 9,
      "Y": -3,
      "Side": "South"
    },
    {
      "X": 9,
      "Y": -6,
      "Side": "North"
    },
    {
      "X": 8,
      "Y": -3,
      "Side": "South"
    },
    {
      "X": 8,
      "Y": -6,
      "Side": "North"
    },
    {
      "X": 21,
      "Y": -3,
      "Side": "South"
    },
    {
      "X": 21,
      "Y": -6,
      "Side": "North"
    },
    {
      "X": 20,
      "Y": -3,
      "Side": "South"
    },
    {
      "X": 20,
      "Y": -6,
      "Side": "North"
    },
    {
      "X": 19,
      "Y": -4,
      "Side": "West"
    },
    {
      "X": 15,
      "Y": -17,
      "Side": "West"
    },
    {
      "X": 15,
      "Y": -16,
      "Side": "West"
    },
    {
      "X": 18,
      "Y": -17,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": -16,
      "Side": "East"
    },
    {
      "X": 11,
      "Y": -17,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": -16,
      "Side": "West"
    },
    {
      "X": 14,
      "Y": -17,
      "Side": "East"
    },
    {
      "X": 14,
      "Y": -16,
      "Side": "East"
    },
    {
      "X": 30,
      "Y": 4,
      "Side": "East"
    },
    {
      "X": 29,
      "Y": 5,
      "Side": "South"
    },
    {
      "X": 29,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 28,
      "Y": 5,
      "Side": "South"
    },
    {
      "X": 28,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 27,
      "Y": 3,
      "Side": "West"
    },
    {
      "X": 0,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 0,
      "Y": 5,
      "Side": "South"
    },
    {
      "X": 1,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 1,
      "Y": 5,
      "Side": "South"
    },
    {
      "X": 2,
      "Y": 3,
      "Side": "East"
    },
    {
      "X": 19,
      "Y": 11,
      "Side": "West"
    },
    {
      "X": 19,
      "Y": 12,
      "Side": "West"
    },
    {
      "X": 20,
      "Y": 10,
      "Side": "North"
    },
    {
      "X": 22,
      "Y": 11,
      "Side": "East"
    },
    {
      "X": 22,
      "Y": 12,
      "Side": "East"
    },
    {
      "X": 23,
      "Y": 11,
      "Side": "West"
    },
    {
      "X": 23,
      "Y": 12,
      "Side": "West"
    },
    {
      "X": 25,
      "Y": 10,
      "Side": "North"
    },
    {
      "X": 26,
      "Y": 11,
      "Side": "East"
    },
    {
      "X": 26,
      "Y": 12,
      "Side": "East"
    },
    {
      "X": 7,
      "Y": 11,
      "Side": "West"
    },
    {
      "X": 7,
      "Y": 12,
      "Side": "West"
    },
    {
      "X": 9,
      "Y": 10,
      "Side": "North"
    },
    {
      "X": 10,
      "Y": 11,
      "Side": "East"
    },
    {
      "X": 10,
      "Y": 12,
      "Side": "East"
    },
    {
      "X": 3,
      "Y": 11,
      "Side": "West"
    },
    {
      "X": 3,
      "Y": 12,
      "Side": "West"
    },
    {
      "X": 4,
      "Y": 10,
      "Side": "North"
    },
    {
      "X": 6,
      "Y": 11,
      "Side": "East"
    },
    {
      "X": 6,
      "Y": 12,
      "Side": "East"
    }
  ],
  "DoorEdgesRaw": [
    {
      "X": 14,
      "Y": -7,
      "Side": "South",
      "Id": "door-edge-0"
    },
    {
      "X": 15,
      "Y": -7,
      "Side": "South",
      "Id": "door-edge-0"
    },
    {
      "X": 16,
      "Y": -15,
      "Side": "South",
      "Id": "door-edge-1"
    },
    {
      "X": 17,
      "Y": -15,
      "Side": "South",
      "Id": "door-edge-1"
    },
    {
      "X": 12,
      "Y": -15,
      "Side": "South",
      "Id": "door-edge-2"
    },
    {
      "X": 13,
      "Y": -15,
      "Side": "South",
      "Id": "door-edge-2"
    },
    {
      "X": 14,
      "Y": 1,
      "Side": "South",
      "Id": "door-edge-3"
    },
    {
      "X": 15,
      "Y": 1,
      "Side": "South",
      "Id": "door-edge-3"
    },
    {
      "X": 18,
      "Y": 5,
      "Side": "East",
      "Id": "door-edge-4"
    },
    {
      "X": 18,
      "Y": 6,
      "Side": "East",
      "Id": "door-edge-4"
    },
    {
      "X": 10,
      "Y": 5,
      "Side": "East",
      "Id": "door-edge-5"
    },
    {
      "X": 10,
      "Y": 6,
      "Side": "East",
      "Id": "door-edge-5"
    },
    {
      "X": 18,
      "Y": -5,
      "Side": "East",
      "Id": "door-edge-10"
    },
    {
      "X": 10,
      "Y": -5,
      "Side": "East",
      "Id": "door-edge-14"
    },
    {
      "X": 10,
      "Y": -1,
      "Side": "East",
      "Id": "door-edge-8"
    },
    {
      "X": 18,
      "Y": -1,
      "Side": "East",
      "Id": "door-edge-9"
    },
    {
      "X": 24,
      "Y": 9,
      "Side": "South",
      "Id": "door-edge-15"
    },
    {
      "X": 21,
      "Y": 9,
      "Side": "South",
      "Id": "door-edge-17"
    },
    {
      "X": 8,
      "Y": 9,
      "Side": "South",
      "Id": "door-edge-18"
    },
    {
      "X": 5,
      "Y": 9,
      "Side": "South",
      "Id": "door-edge-19"
    },
    {
      "X": 26,
      "Y": 4,
      "Side": "East",
      "Id": "door-edge-22"
    },
    {
      "X": 2,
      "Y": 4,
      "Side": "East",
      "Id": "door-edge-21"
    }
  ],
  "WreckPatchesRaw": null,
  "WallMaterials": [],
  "Engines": [
    {
      "X": 8.5,
      "Y": -4.5,
      "Facing": "West",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 8.5,
      "Y": -3.5,
      "Facing": "West",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 21.5,
      "Y": -3.5,
      "Facing": "East",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 21.5,
      "Y": -4.5,
      "Facing": "East",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 17.5,
      "Y": -16.5,
      "Facing": "North",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 16.5,
      "Y": -16.5,
      "Facing": "North",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 13.5,
      "Y": -16.5,
      "Facing": "North",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 12.5,
      "Y": -16.5,
      "Facing": "North",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 0.5,
      "Y": 3.5,
      "Facing": "West",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 0.5,
      "Y": 4.5,
      "Facing": "West",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 21.5,
      "Y": 12.5,
      "Facing": "South",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 20.5,
      "Y": 12.5,
      "Facing": "South",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 24.5,
      "Y": 12.5,
      "Facing": "South",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 25.5,
      "Y": 12.5,
      "Facing": "South",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 8.5,
      "Y": 12.5,
      "Facing": "South",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 9.5,
      "Y": 12.5,
      "Facing": "South",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 4.5,
      "Y": 12.5,
      "Facing": "South",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 5.5,
      "Y": 12.5,
      "Facing": "South",
      "MaxThrust": 8,
      "Role": "Marching"
    }
  ],
  "SupplementalWallTiles": [
    {
      "X": 13,
      "Y": 4
    },
    {
      "X": 16,
      "Y": 4
    },
    {
      "X": 18,
      "Y": 3
    },
    {
      "X": 18,
      "Y": 4
    },
    {
      "X": 18,
      "Y": 7
    },
    {
      "X": 18,
      "Y": 8
    },
    {
      "X": 12,
      "Y": 1
    },
    {
      "X": 13,
      "Y": 1
    },
    {
      "X": 16,
      "Y": 1
    },
    {
      "X": 17,
      "Y": 1
    },
    {
      "X": 18,
      "Y": -4
    },
    {
      "X": 18,
      "Y": -3
    },
    {
      "X": 18,
      "Y": -2
    },
    {
      "X": 18,
      "Y": 0
    },
    {
      "X": 18,
      "Y": 1
    },
    {
      "X": 20,
      "Y": 9
    },
    {
      "X": 21,
      "Y": 5
    },
    {
      "X": 22,
      "Y": 5
    },
    {
      "X": 22,
      "Y": 9
    },
    {
      "X": 23,
      "Y": 5
    },
    {
      "X": 23,
      "Y": 9
    },
    {
      "X": 24,
      "Y": 5
    },
    {
      "X": 25,
      "Y": 9
    },
    {
      "X": 26,
      "Y": 3
    },
    {
      "X": 26,
      "Y": 5
    },
    {
      "X": 4,
      "Y": 9
    },
    {
      "X": 5,
      "Y": 5
    },
    {
      "X": 6,
      "Y": 5
    },
    {
      "X": 6,
      "Y": 9
    },
    {
      "X": 7,
      "Y": 5
    },
    {
      "X": 7,
      "Y": 9
    },
    {
      "X": 8,
      "Y": 5
    },
    {
      "X": 9,
      "Y": 9
    },
    {
      "X": 10,
      "Y": 3
    },
    {
      "X": 10,
      "Y": 4
    },
    {
      "X": 10,
      "Y": 7
    },
    {
      "X": 10,
      "Y": 8
    },
    {
      "X": 10,
      "Y": 9
    },
    {
      "X": 17,
      "Y": -7
    },
    {
      "X": 16,
      "Y": -7
    },
    {
      "X": 14,
      "Y": -9
    },
    {
      "X": 14,
      "Y": -10
    },
    {
      "X": 14,
      "Y": -11
    },
    {
      "X": 14,
      "Y": -12
    },
    {
      "X": 13,
      "Y": -7
    },
    {
      "X": 12,
      "Y": -7
    },
    {
      "X": 21,
      "Y": 1
    },
    {
      "X": 20,
      "Y": 1
    },
    {
      "X": 8,
      "Y": 1
    },
    {
      "X": 9,
      "Y": 1
    },
    {
      "X": 10,
      "Y": 0
    },
    {
      "X": 10,
      "Y": 1
    },
    {
      "X": 10,
      "Y": -3
    },
    {
      "X": 10,
      "Y": -4
    },
    {
      "X": 9,
      "Y": -3
    },
    {
      "X": 8,
      "Y": -3
    },
    {
      "X": 21,
      "Y": -3
    },
    {
      "X": 20,
      "Y": -3
    },
    {
      "X": 14,
      "Y": -17
    },
    {
      "X": 14,
      "Y": -16
    },
    {
      "X": 14,
      "Y": -15
    },
    {
      "X": 2,
      "Y": 3
    },
    {
      "X": 22,
      "Y": 11
    },
    {
      "X": 22,
      "Y": 12
    },
    {
      "X": 6,
      "Y": 11
    },
    {
      "X": 6,
      "Y": 12
    }
  ],
  "ForcedFloorTiles": [
    {
      "X": 15,
      "Y": -7
    },
    {
      "X": 15,
      "Y": -8
    },
    {
      "X": 14,
      "Y": -7
    },
    {
      "X": 15,
      "Y": -9
    },
    {
      "X": 16,
      "Y": -8
    },
    {
      "X": 14,
      "Y": -8
    },
    {
      "X": 15,
      "Y": -10
    },
    {
      "X": 16,
      "Y": -9
    },
    {
      "X": 17,
      "Y": -8
    },
    {
      "X": 13,
      "Y": -8
    },
    {
      "X": 15,
      "Y": -11
    },
    {
      "X": 16,
      "Y": -10
    },
    {
      "X": 17,
      "Y": -9
    },
    {
      "X": 13,
      "Y": -9
    },
    {
      "X": 12,
      "Y": -8
    },
    {
      "X": 15,
      "Y": -12
    },
    {
      "X": 16,
      "Y": -11
    },
    {
      "X": 17,
      "Y": -10
    },
    {
      "X": 13,
      "Y": -10
    },
    {
      "X": 12,
      "Y": -9
    },
    {
      "X": 15,
      "Y": -13
    },
    {
      "X": 16,
      "Y": -12
    },
    {
      "X": 17,
      "Y": -11
    },
    {
      "X": 13,
      "Y": -11
    },
    {
      "X": 12,
      "Y": -10
    },
    {
      "X": 16,
      "Y": -13
    },
    {
      "X": 14,
      "Y": -13
    },
    {
      "X": 17,
      "Y": -12
    },
    {
      "X": 13,
      "Y": -12
    },
    {
      "X": 12,
      "Y": -11
    },
    {
      "X": 16,
      "Y": -14
    },
    {
      "X": 17,
      "Y": -13
    },
    {
      "X": 13,
      "Y": -13
    },
    {
      "X": 12,
      "Y": -12
    },
    {
      "X": 17,
      "Y": -14
    },
    {
      "X": 13,
      "Y": -14
    },
    {
      "X": 12,
      "Y": -13
    },
    {
      "X": 12,
      "Y": -14
    },
    {
      "X": 28,
      "Y": 4
    },
    {
      "X": 28,
      "Y": 3
    },
    {
      "X": 29,
      "Y": 4
    },
    {
      "X": 27,
      "Y": 4
    },
    {
      "X": 29,
      "Y": 3
    },
    {
      "X": 16,
      "Y": -15
    },
    {
      "X": 16,
      "Y": -16
    },
    {
      "X": 17,
      "Y": -15
    },
    {
      "X": 16,
      "Y": -17
    },
    {
      "X": 17,
      "Y": -16
    },
    {
      "X": 17,
      "Y": -17
    },
    {
      "X": 13,
      "Y": -15
    },
    {
      "X": 13,
      "Y": -16
    },
    {
      "X": 12,
      "Y": -15
    },
    {
      "X": 13,
      "Y": -17
    },
    {
      "X": 12,
      "Y": -16
    },
    {
      "X": 12,
      "Y": -17
    },
    {
      "X": 15,
      "Y": 1
    },
    {
      "X": 15,
      "Y": 0
    },
    {
      "X": 14,
      "Y": 1
    },
    {
      "X": 15,
      "Y": -1
    },
    {
      "X": 16,
      "Y": 0
    },
    {
      "X": 14,
      "Y": 0
    },
    {
      "X": 15,
      "Y": -2
    },
    {
      "X": 16,
      "Y": -1
    },
    {
      "X": 14,
      "Y": -1
    },
    {
      "X": 17,
      "Y": 0
    },
    {
      "X": 13,
      "Y": 0
    },
    {
      "X": 15,
      "Y": -3
    },
    {
      "X": 16,
      "Y": -2
    },
    {
      "X": 14,
      "Y": -2
    },
    {
      "X": 17,
      "Y": -1
    },
    {
      "X": 13,
      "Y": -1
    },
    {
      "X": 12,
      "Y": 0
    },
    {
      "X": 15,
      "Y": -4
    },
    {
      "X": 16,
      "Y": -3
    },
    {
      "X": 14,
      "Y": -3
    },
    {
      "X": 17,
      "Y": -2
    },
    {
      "X": 13,
      "Y": -2
    },
    {
      "X": 18,
      "Y": -1
    },
    {
      "X": 12,
      "Y": -1
    },
    {
      "X": 15,
      "Y": -5
    },
    {
      "X": 16,
      "Y": -4
    },
    {
      "X": 14,
      "Y": -4
    },
    {
      "X": 17,
      "Y": -3
    },
    {
      "X": 13,
      "Y": -3
    },
    {
      "X": 12,
      "Y": -2
    },
    {
      "X": 11,
      "Y": -1
    },
    {
      "X": 15,
      "Y": -6
    },
    {
      "X": 16,
      "Y": -5
    },
    {
      "X": 14,
      "Y": -5
    },
    {
      "X": 17,
      "Y": -4
    },
    {
      "X": 13,
      "Y": -4
    },
    {
      "X": 12,
      "Y": -3
    },
    {
      "X": 14,
      "Y": -6
    },
    {
      "X": 17,
      "Y": -5
    },
    {
      "X": 13,
      "Y": -5
    },
    {
      "X": 12,
      "Y": -4
    },
    {
      "X": 18,
      "Y": -5
    },
    {
      "X": 12,
      "Y": -5
    },
    {
      "X": 11,
      "Y": -5
    },
    {
      "X": 18,
      "Y": 5
    },
    {
      "X": 18,
      "Y": 6
    },
    {
      "X": 17,
      "Y": 5
    },
    {
      "X": 17,
      "Y": 6
    },
    {
      "X": 17,
      "Y": 4
    },
    {
      "X": 16,
      "Y": 5
    },
    {
      "X": 17,
      "Y": 7
    },
    {
      "X": 16,
      "Y": 6
    },
    {
      "X": 17,
      "Y": 3
    },
    {
      "X": 15,
      "Y": 5
    },
    {
      "X": 17,
      "Y": 8
    },
    {
      "X": 16,
      "Y": 7
    },
    {
      "X": 15,
      "Y": 6
    },
    {
      "X": 16,
      "Y": 3
    },
    {
      "X": 15,
      "Y": 4
    },
    {
      "X": 14,
      "Y": 5
    },
    {
      "X": 16,
      "Y": 8
    },
    {
      "X": 15,
      "Y": 7
    },
    {
      "X": 14,
      "Y": 6
    },
    {
      "X": 15,
      "Y": 3
    },
    {
      "X": 14,
      "Y": 4
    },
    {
      "X": 13,
      "Y": 5
    },
    {
      "X": 15,
      "Y": 8
    },
    {
      "X": 14,
      "Y": 7
    },
    {
      "X": 13,
      "Y": 6
    },
    {
      "X": 15,
      "Y": 2
    },
    {
      "X": 14,
      "Y": 3
    },
    {
      "X": 12,
      "Y": 5
    },
    {
      "X": 14,
      "Y": 8
    },
    {
      "X": 13,
      "Y": 7
    },
    {
      "X": 12,
      "Y": 6
    },
    {
      "X": 14,
      "Y": 2
    },
    {
      "X": 13,
      "Y": 3
    },
    {
      "X": 12,
      "Y": 4
    },
    {
      "X": 11,
      "Y": 5
    },
    {
      "X": 13,
      "Y": 8
    },
    {
      "X": 12,
      "Y": 7
    },
    {
      "X": 11,
      "Y": 6
    },
    {
      "X": 12,
      "Y": 3
    },
    {
      "X": 12,
      "Y": 8
    },
    {
      "X": 10,
      "Y": 5
    },
    {
      "X": 10,
      "Y": 6
    },
    {
      "X": 9,
      "Y": 5
    },
    {
      "X": 9,
      "Y": 6
    },
    {
      "X": 9,
      "Y": 4
    },
    {
      "X": 9,
      "Y": 7
    },
    {
      "X": 8,
      "Y": 6
    },
    {
      "X": 9,
      "Y": 3
    },
    {
      "X": 8,
      "Y": 4
    },
    {
      "X": 9,
      "Y": 8
    },
    {
      "X": 8,
      "Y": 7
    },
    {
      "X": 7,
      "Y": 6
    },
    {
      "X": 8,
      "Y": 3
    },
    {
      "X": 7,
      "Y": 4
    },
    {
      "X": 8,
      "Y": 8
    },
    {
      "X": 7,
      "Y": 7
    },
    {
      "X": 6,
      "Y": 6
    },
    {
      "X": 7,
      "Y": 3
    },
    {
      "X": 6,
      "Y": 4
    },
    {
      "X": 8,
      "Y": 9
    },
    {
      "X": 7,
      "Y": 8
    },
    {
      "X": 6,
      "Y": 7
    },
    {
      "X": 5,
      "Y": 6
    },
    {
      "X": 6,
      "Y": 3
    },
    {
      "X": 5,
      "Y": 4
    },
    {
      "X": 6,
      "Y": 8
    },
    {
      "X": 5,
      "Y": 7
    },
    {
      "X": 4,
      "Y": 6
    },
    {
      "X": 5,
      "Y": 3
    },
    {
      "X": 4,
      "Y": 4
    },
    {
      "X": 5,
      "Y": 8
    },
    {
      "X": 4,
      "Y": 7
    },
    {
      "X": 4,
      "Y": 5
    },
    {
      "X": 4,
      "Y": 3
    },
    {
      "X": 3,
      "Y": 4
    },
    {
      "X": 5,
      "Y": 9
    },
    {
      "X": 4,
      "Y": 8
    },
    {
      "X": 19,
      "Y": -5
    },
    {
      "X": 20,
      "Y": -5
    },
    {
      "X": 20,
      "Y": -4
    },
    {
      "X": 21,
      "Y": -5
    },
    {
      "X": 21,
      "Y": -4
    },
    {
      "X": 10,
      "Y": -5
    },
    {
      "X": 9,
      "Y": -5
    },
    {
      "X": 9,
      "Y": -4
    },
    {
      "X": 8,
      "Y": -5
    },
    {
      "X": 8,
      "Y": -4
    },
    {
      "X": 10,
      "Y": -1
    },
    {
      "X": 9,
      "Y": -1
    },
    {
      "X": 9,
      "Y": 0
    },
    {
      "X": 8,
      "Y": -1
    },
    {
      "X": 8,
      "Y": 0
    },
    {
      "X": 19,
      "Y": -1
    },
    {
      "X": 20,
      "Y": -1
    },
    {
      "X": 20,
      "Y": 0
    },
    {
      "X": 21,
      "Y": -1
    },
    {
      "X": 21,
      "Y": 0
    },
    {
      "X": 24,
      "Y": 10
    },
    {
      "X": 24,
      "Y": 11
    },
    {
      "X": 24,
      "Y": 12
    },
    {
      "X": 25,
      "Y": 11
    },
    {
      "X": 25,
      "Y": 12
    },
    {
      "X": 21,
      "Y": 10
    },
    {
      "X": 21,
      "Y": 11
    },
    {
      "X": 21,
      "Y": 12
    },
    {
      "X": 20,
      "Y": 11
    },
    {
      "X": 20,
      "Y": 12
    },
    {
      "X": 8,
      "Y": 10
    },
    {
      "X": 8,
      "Y": 11
    },
    {
      "X": 8,
      "Y": 12
    },
    {
      "X": 9,
      "Y": 11
    },
    {
      "X": 9,
      "Y": 12
    },
    {
      "X": 5,
      "Y": 10
    },
    {
      "X": 5,
      "Y": 11
    },
    {
      "X": 5,
      "Y": 12
    },
    {
      "X": 4,
      "Y": 11
    },
    {
      "X": 4,
      "Y": 12
    },
    {
      "X": 26,
      "Y": 4
    },
    {
      "X": 25,
      "Y": 4
    },
    {
      "X": 25,
      "Y": 3
    },
    {
      "X": 25,
      "Y": 5
    },
    {
      "X": 24,
      "Y": 4
    },
    {
      "X": 24,
      "Y": 3
    },
    {
      "X": 25,
      "Y": 6
    },
    {
      "X": 23,
      "Y": 4
    },
    {
      "X": 23,
      "Y": 3
    },
    {
      "X": 25,
      "Y": 7
    },
    {
      "X": 24,
      "Y": 6
    },
    {
      "X": 22,
      "Y": 4
    },
    {
      "X": 22,
      "Y": 3
    },
    {
      "X": 25,
      "Y": 8
    },
    {
      "X": 24,
      "Y": 7
    },
    {
      "X": 23,
      "Y": 6
    },
    {
      "X": 21,
      "Y": 4
    },
    {
      "X": 21,
      "Y": 3
    },
    {
      "X": 24,
      "Y": 8
    },
    {
      "X": 23,
      "Y": 7
    },
    {
      "X": 22,
      "Y": 6
    },
    {
      "X": 20,
      "Y": 4
    },
    {
      "X": 20,
      "Y": 3
    },
    {
      "X": 24,
      "Y": 9
    },
    {
      "X": 23,
      "Y": 8
    },
    {
      "X": 22,
      "Y": 7
    },
    {
      "X": 21,
      "Y": 6
    },
    {
      "X": 20,
      "Y": 5
    },
    {
      "X": 22,
      "Y": 8
    },
    {
      "X": 21,
      "Y": 7
    },
    {
      "X": 20,
      "Y": 6
    },
    {
      "X": 19,
      "Y": 5
    },
    {
      "X": 21,
      "Y": 8
    },
    {
      "X": 20,
      "Y": 7
    },
    {
      "X": 19,
      "Y": 6
    },
    {
      "X": 21,
      "Y": 9
    },
    {
      "X": 20,
      "Y": 8
    },
    {
      "X": 2,
      "Y": 4
    },
    {
      "X": 1,
      "Y": 4
    },
    {
      "X": 1,
      "Y": 3
    },
    {
      "X": 0,
      "Y": 4
    },
    {
      "X": 0,
      "Y": 3
    }
  ],
  "WallOpenSides": [
    {
      "X": 11,
      "Y": 3,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": 4,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": 7,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": 8,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 12,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 13,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 13,
      "Y": 4,
      "Side": "South"
    },
    {
      "X": 13,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 14,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 15,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 16,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 16,
      "Y": 4,
      "Side": "South"
    },
    {
      "X": 16,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 17,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 17,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 18,
      "Y": 3,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": 4,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": 7,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": 8,
      "Side": "East"
    },
    {
      "X": 11,
      "Y": -4,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": -3,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": -2,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": 0,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": -6,
      "Side": "North"
    },
    {
      "X": 12,
      "Y": 1,
      "Side": "South"
    },
    {
      "X": 13,
      "Y": -6,
      "Side": "North"
    },
    {
      "X": 13,
      "Y": 1,
      "Side": "South"
    },
    {
      "X": 16,
      "Y": -6,
      "Side": "North"
    },
    {
      "X": 16,
      "Y": 1,
      "Side": "South"
    },
    {
      "X": 17,
      "Y": -6,
      "Side": "North"
    },
    {
      "X": 17,
      "Y": 1,
      "Side": "South"
    },
    {
      "X": 18,
      "Y": -4,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": -3,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": -2,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": 0,
      "Side": "East"
    },
    {
      "X": 19,
      "Y": 3,
      "Side": "West"
    },
    {
      "X": 19,
      "Y": 4,
      "Side": "West"
    },
    {
      "X": 19,
      "Y": 7,
      "Side": "West"
    },
    {
      "X": 19,
      "Y": 8,
      "Side": "West"
    },
    {
      "X": 20,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 20,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 21,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 21,
      "Y": 5,
      "Side": "South"
    },
    {
      "X": 22,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 22,
      "Y": 5,
      "Side": "South"
    },
    {
      "X": 22,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 23,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 23,
      "Y": 5,
      "Side": "South"
    },
    {
      "X": 23,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 24,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 24,
      "Y": 5,
      "Side": "South"
    },
    {
      "X": 25,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 25,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 26,
      "Y": 3,
      "Side": "East"
    },
    {
      "X": 26,
      "Y": 5,
      "Side": "East"
    },
    {
      "X": 26,
      "Y": 6,
      "Side": "East"
    },
    {
      "X": 26,
      "Y": 7,
      "Side": "East"
    },
    {
      "X": 26,
      "Y": 8,
      "Side": "East"
    },
    {
      "X": 3,
      "Y": 3,
      "Side": "West"
    },
    {
      "X": 3,
      "Y": 5,
      "Side": "West"
    },
    {
      "X": 3,
      "Y": 6,
      "Side": "West"
    },
    {
      "X": 3,
      "Y": 7,
      "Side": "West"
    },
    {
      "X": 3,
      "Y": 8,
      "Side": "West"
    },
    {
      "X": 4,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 4,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 5,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 5,
      "Y": 5,
      "Side": "South"
    },
    {
      "X": 6,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 6,
      "Y": 5,
      "Side": "South"
    },
    {
      "X": 6,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 7,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 7,
      "Y": 5,
      "Side": "South"
    },
    {
      "X": 7,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 8,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 8,
      "Y": 5,
      "Side": "South"
    },
    {
      "X": 9,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 9,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 10,
      "Y": 3,
      "Side": "East"
    },
    {
      "X": 10,
      "Y": 4,
      "Side": "East"
    },
    {
      "X": 10,
      "Y": 7,
      "Side": "East"
    },
    {
      "X": 10,
      "Y": 8,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": -8,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": -9,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": -10,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": -11,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": -12,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": -13,
      "Side": "East"
    },
    {
      "X": 17,
      "Y": -7,
      "Side": "South"
    },
    {
      "X": 16,
      "Y": -7,
      "Side": "South"
    },
    {
      "X": 15,
      "Y": -14,
      "Side": "North"
    },
    {
      "X": 14,
      "Y": -9,
      "Side": "East"
    },
    {
      "X": 14,
      "Y": -10,
      "Side": "East"
    },
    {
      "X": 14,
      "Y": -11,
      "Side": "East"
    },
    {
      "X": 14,
      "Y": -12,
      "Side": "East"
    },
    {
      "X": 14,
      "Y": -14,
      "Side": "North"
    },
    {
      "X": 13,
      "Y": -7,
      "Side": "South"
    },
    {
      "X": 12,
      "Y": -7,
      "Side": "South"
    },
    {
      "X": 11,
      "Y": -8,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": -9,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": -10,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": -11,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": -12,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": -13,
      "Side": "West"
    },
    {
      "X": 22,
      "Y": 0,
      "Side": "East"
    },
    {
      "X": 22,
      "Y": -1,
      "Side": "East"
    },
    {
      "X": 21,
      "Y": 1,
      "Side": "South"
    },
    {
      "X": 21,
      "Y": -2,
      "Side": "North"
    },
    {
      "X": 20,
      "Y": 1,
      "Side": "South"
    },
    {
      "X": 20,
      "Y": -2,
      "Side": "North"
    },
    {
      "X": 19,
      "Y": 0,
      "Side": "West"
    },
    {
      "X": 7,
      "Y": -1,
      "Side": "West"
    },
    {
      "X": 7,
      "Y": 0,
      "Side": "West"
    },
    {
      "X": 8,
      "Y": -2,
      "Side": "North"
    },
    {
      "X": 8,
      "Y": 1,
      "Side": "South"
    },
    {
      "X": 9,
      "Y": -2,
      "Side": "North"
    },
    {
      "X": 9,
      "Y": 1,
      "Side": "South"
    },
    {
      "X": 10,
      "Y": 0,
      "Side": "East"
    },
    {
      "X": 10,
      "Y": -4,
      "Side": "East"
    },
    {
      "X": 9,
      "Y": -3,
      "Side": "South"
    },
    {
      "X": 9,
      "Y": -6,
      "Side": "North"
    },
    {
      "X": 8,
      "Y": -3,
      "Side": "South"
    },
    {
      "X": 8,
      "Y": -6,
      "Side": "North"
    },
    {
      "X": 21,
      "Y": -3,
      "Side": "South"
    },
    {
      "X": 21,
      "Y": -6,
      "Side": "North"
    },
    {
      "X": 20,
      "Y": -3,
      "Side": "South"
    },
    {
      "X": 20,
      "Y": -6,
      "Side": "North"
    },
    {
      "X": 19,
      "Y": -4,
      "Side": "West"
    },
    {
      "X": 15,
      "Y": -17,
      "Side": "West"
    },
    {
      "X": 15,
      "Y": -16,
      "Side": "West"
    },
    {
      "X": 18,
      "Y": -17,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": -16,
      "Side": "East"
    },
    {
      "X": 11,
      "Y": -17,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": -16,
      "Side": "West"
    },
    {
      "X": 14,
      "Y": -17,
      "Side": "East"
    },
    {
      "X": 14,
      "Y": -16,
      "Side": "East"
    },
    {
      "X": 30,
      "Y": 4,
      "Side": "East"
    },
    {
      "X": 29,
      "Y": 5,
      "Side": "South"
    },
    {
      "X": 29,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 28,
      "Y": 5,
      "Side": "South"
    },
    {
      "X": 28,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 27,
      "Y": 3,
      "Side": "West"
    },
    {
      "X": 0,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 0,
      "Y": 5,
      "Side": "South"
    },
    {
      "X": 1,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 1,
      "Y": 5,
      "Side": "South"
    },
    {
      "X": 2,
      "Y": 3,
      "Side": "East"
    },
    {
      "X": 19,
      "Y": 11,
      "Side": "West"
    },
    {
      "X": 19,
      "Y": 12,
      "Side": "West"
    },
    {
      "X": 20,
      "Y": 10,
      "Side": "North"
    },
    {
      "X": 22,
      "Y": 11,
      "Side": "East"
    },
    {
      "X": 22,
      "Y": 12,
      "Side": "East"
    },
    {
      "X": 23,
      "Y": 11,
      "Side": "West"
    },
    {
      "X": 23,
      "Y": 12,
      "Side": "West"
    },
    {
      "X": 25,
      "Y": 10,
      "Side": "North"
    },
    {
      "X": 26,
      "Y": 11,
      "Side": "East"
    },
    {
      "X": 26,
      "Y": 12,
      "Side": "East"
    },
    {
      "X": 7,
      "Y": 11,
      "Side": "West"
    },
    {
      "X": 7,
      "Y": 12,
      "Side": "West"
    },
    {
      "X": 9,
      "Y": 10,
      "Side": "North"
    },
    {
      "X": 10,
      "Y": 11,
      "Side": "East"
    },
    {
      "X": 10,
      "Y": 12,
      "Side": "East"
    },
    {
      "X": 3,
      "Y": 11,
      "Side": "West"
    },
    {
      "X": 3,
      "Y": 12,
      "Side": "West"
    },
    {
      "X": 4,
      "Y": 10,
      "Side": "North"
    },
    {
      "X": 6,
      "Y": 11,
      "Side": "East"
    },
    {
      "X": 6,
      "Y": 12,
      "Side": "East"
    }
  ],
  "DoorEdges": [
    {
      "X": 14,
      "Y": -7,
      "Side": "South",
      "Id": "door-edge-0"
    },
    {
      "X": 15,
      "Y": -7,
      "Side": "South",
      "Id": "door-edge-0"
    },
    {
      "X": 16,
      "Y": -15,
      "Side": "South",
      "Id": "door-edge-1"
    },
    {
      "X": 17,
      "Y": -15,
      "Side": "South",
      "Id": "door-edge-1"
    },
    {
      "X": 12,
      "Y": -15,
      "Side": "South",
      "Id": "door-edge-2"
    },
    {
      "X": 13,
      "Y": -15,
      "Side": "South",
      "Id": "door-edge-2"
    },
    {
      "X": 14,
      "Y": 1,
      "Side": "South",
      "Id": "door-edge-3"
    },
    {
      "X": 15,
      "Y": 1,
      "Side": "South",
      "Id": "door-edge-3"
    },
    {
      "X": 18,
      "Y": 5,
      "Side": "East",
      "Id": "door-edge-4"
    },
    {
      "X": 18,
      "Y": 6,
      "Side": "East",
      "Id": "door-edge-4"
    },
    {
      "X": 10,
      "Y": 5,
      "Side": "East",
      "Id": "door-edge-5"
    },
    {
      "X": 10,
      "Y": 6,
      "Side": "East",
      "Id": "door-edge-5"
    },
    {
      "X": 18,
      "Y": -5,
      "Side": "East",
      "Id": "door-edge-10"
    },
    {
      "X": 10,
      "Y": -5,
      "Side": "East",
      "Id": "door-edge-14"
    },
    {
      "X": 10,
      "Y": -1,
      "Side": "East",
      "Id": "door-edge-8"
    },
    {
      "X": 18,
      "Y": -1,
      "Side": "East",
      "Id": "door-edge-9"
    },
    {
      "X": 24,
      "Y": 9,
      "Side": "South",
      "Id": "door-edge-15"
    },
    {
      "X": 21,
      "Y": 9,
      "Side": "South",
      "Id": "door-edge-17"
    },
    {
      "X": 8,
      "Y": 9,
      "Side": "South",
      "Id": "door-edge-18"
    },
    {
      "X": 5,
      "Y": 9,
      "Side": "South",
      "Id": "door-edge-19"
    },
    {
      "X": 26,
      "Y": 4,
      "Side": "East",
      "Id": "door-edge-22"
    },
    {
      "X": 2,
      "Y": 4,
      "Side": "East",
      "Id": "door-edge-21"
    }
  ],
  "WreckPatches": []
}
""";
}
