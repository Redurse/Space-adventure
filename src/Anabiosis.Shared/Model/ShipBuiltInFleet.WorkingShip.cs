namespace Anabiosis.Shared.Model;

// Raw JSON captured verbatim from %LocalAppData%\Anabiosis\custom-ships\рабочий корабль.json -
// see ShipBuiltInFleet.cs's own doc comment for why this is frozen here instead of read from disk.
public static partial class ShipBuiltInFleet
{
    private const string WorkingShipJson = """
{
  "Name": "\u0420\u0435\u0430\u043A\u0442\u043E\u0440\u043D\u044B\u0439 \u043E\u0442\u0441\u0435\u043A",
  "Rooms": [
    {
      "Id": "room-61",
      "Name": "\u041E\u0442\u0441\u0435\u043A 61",
      "Rects": [
        {
          "X": 31,
          "Y": 13,
          "Width": 2,
          "Height": 6,
          "Left": 31,
          "Right": 33,
          "Top": 13,
          "Bottom": 19,
          "Area": 12,
          "Center": {
            "X": 32,
            "Y": 16
          }
        },
        {
          "X": 30,
          "Y": 17,
          "Width": 1,
          "Height": 1,
          "Left": 30,
          "Right": 31,
          "Top": 17,
          "Bottom": 18,
          "Area": 1,
          "Center": {
            "X": 30.5,
            "Y": 17.5
          }
        }
      ],
      "X": 30,
      "Y": 13,
      "Width": 3,
      "Height": 6
    },
    {
      "Id": "room-63",
      "Name": "\u041E\u0442\u0441\u0435\u043A 63",
      "Rects": [
        {
          "X": 0,
          "Y": 10,
          "Width": 4,
          "Height": 2,
          "Left": 0,
          "Right": 4,
          "Top": 10,
          "Bottom": 12,
          "Area": 8,
          "Center": {
            "X": 2,
            "Y": 11
          }
        },
        {
          "X": 1,
          "Y": 12,
          "Width": 1,
          "Height": 1,
          "Left": 1,
          "Right": 2,
          "Top": 12,
          "Bottom": 13,
          "Area": 1,
          "Center": {
            "X": 1.5,
            "Y": 12.5
          }
        }
      ],
      "X": 0,
      "Y": 10,
      "Width": 4,
      "Height": 3
    },
    {
      "Id": "room-59",
      "Name": "\u041E\u0442\u0441\u0435\u043A 59",
      "Rects": [
        {
          "X": 24,
          "Y": 10,
          "Width": 3,
          "Height": 11,
          "Left": 24,
          "Right": 27,
          "Top": 10,
          "Bottom": 21,
          "Area": 33,
          "Center": {
            "X": 25.5,
            "Y": 15.5
          }
        },
        {
          "X": 23,
          "Y": 13,
          "Width": 1,
          "Height": 6,
          "Left": 23,
          "Right": 24,
          "Top": 13,
          "Bottom": 19,
          "Area": 6,
          "Center": {
            "X": 23.5,
            "Y": 16
          }
        },
        {
          "X": 27,
          "Y": 13,
          "Width": 1,
          "Height": 6,
          "Left": 27,
          "Right": 28,
          "Top": 13,
          "Bottom": 19,
          "Area": 6,
          "Center": {
            "X": 27.5,
            "Y": 16
          }
        },
        {
          "X": 21,
          "Y": 14,
          "Width": 2,
          "Height": 1,
          "Left": 21,
          "Right": 23,
          "Top": 14,
          "Bottom": 15,
          "Area": 2,
          "Center": {
            "X": 22,
            "Y": 14.5
          }
        },
        {
          "X": 28,
          "Y": 14,
          "Width": 1,
          "Height": 4,
          "Left": 28,
          "Right": 29,
          "Top": 14,
          "Bottom": 18,
          "Area": 4,
          "Center": {
            "X": 28.5,
            "Y": 16
          }
        },
        {
          "X": 22,
          "Y": 15,
          "Width": 1,
          "Height": 3,
          "Left": 22,
          "Right": 23,
          "Top": 15,
          "Bottom": 18,
          "Area": 3,
          "Center": {
            "X": 22.5,
            "Y": 16.5
          }
        },
        {
          "X": 29,
          "Y": 17,
          "Width": 1,
          "Height": 2,
          "Left": 29,
          "Right": 30,
          "Top": 17,
          "Bottom": 19,
          "Area": 2,
          "Center": {
            "X": 29.5,
            "Y": 18
          }
        },
        {
          "X": 23,
          "Y": 21,
          "Width": 3,
          "Height": 1,
          "Left": 23,
          "Right": 26,
          "Top": 21,
          "Bottom": 22,
          "Area": 3,
          "Center": {
            "X": 24.5,
            "Y": 21.5
          }
        }
      ],
      "X": 21,
      "Y": 10,
      "Width": 9,
      "Height": 12
    },
    {
      "Id": "room-28",
      "Name": "\u041E\u0442\u0441\u0435\u043A 28",
      "Rects": [
        {
          "X": 8,
          "Y": 9,
          "Width": 1,
          "Height": 2,
          "Left": 8,
          "Right": 9,
          "Top": 9,
          "Bottom": 11,
          "Area": 2,
          "Center": {
            "X": 8.5,
            "Y": 10
          }
        }
      ],
      "X": 8,
      "Y": 9,
      "Width": 1,
      "Height": 2
    },
    {
      "Id": "room-57",
      "Name": "\u041E\u0442\u0441\u0435\u043A 57",
      "Rects": [
        {
          "X": 6,
          "Y": 11,
          "Width": 3,
          "Height": 10,
          "Left": 6,
          "Right": 9,
          "Top": 11,
          "Bottom": 21,
          "Area": 30,
          "Center": {
            "X": 7.5,
            "Y": 16
          }
        },
        {
          "X": 5,
          "Y": 13,
          "Width": 1,
          "Height": 6,
          "Left": 5,
          "Right": 6,
          "Top": 13,
          "Bottom": 19,
          "Area": 6,
          "Center": {
            "X": 5.5,
            "Y": 16
          }
        },
        {
          "X": 9,
          "Y": 13,
          "Width": 1,
          "Height": 6,
          "Left": 9,
          "Right": 10,
          "Top": 13,
          "Bottom": 19,
          "Area": 6,
          "Center": {
            "X": 9.5,
            "Y": 16
          }
        },
        {
          "X": 4,
          "Y": 14,
          "Width": 1,
          "Height": 4,
          "Left": 4,
          "Right": 5,
          "Top": 14,
          "Bottom": 18,
          "Area": 4,
          "Center": {
            "X": 4.5,
            "Y": 16
          }
        },
        {
          "X": 10,
          "Y": 14,
          "Width": 2,
          "Height": 1,
          "Left": 10,
          "Right": 12,
          "Top": 14,
          "Bottom": 15,
          "Area": 2,
          "Center": {
            "X": 11,
            "Y": 14.5
          }
        },
        {
          "X": 10,
          "Y": 15,
          "Width": 1,
          "Height": 3,
          "Left": 10,
          "Right": 11,
          "Top": 15,
          "Bottom": 18,
          "Area": 3,
          "Center": {
            "X": 10.5,
            "Y": 16.5
          }
        },
        {
          "X": 3,
          "Y": 17,
          "Width": 1,
          "Height": 2,
          "Left": 3,
          "Right": 4,
          "Top": 17,
          "Bottom": 19,
          "Area": 2,
          "Center": {
            "X": 3.5,
            "Y": 18
          }
        },
        {
          "X": 7,
          "Y": 21,
          "Width": 3,
          "Height": 1,
          "Left": 7,
          "Right": 10,
          "Top": 21,
          "Bottom": 22,
          "Area": 3,
          "Center": {
            "X": 8.5,
            "Y": 21.5
          }
        }
      ],
      "X": 3,
      "Y": 11,
      "Width": 9,
      "Height": 11
    },
    {
      "Id": "room-51",
      "Name": "\u041E\u0442\u0441\u0435\u043A 51",
      "Rects": [
        {
          "X": 15,
          "Y": 8,
          "Width": 3,
          "Height": 8,
          "Left": 15,
          "Right": 18,
          "Top": 8,
          "Bottom": 16,
          "Area": 24,
          "Center": {
            "X": 16.5,
            "Y": 12
          }
        },
        {
          "X": 13,
          "Y": 9,
          "Width": 2,
          "Height": 6,
          "Left": 13,
          "Right": 15,
          "Top": 9,
          "Bottom": 15,
          "Area": 12,
          "Center": {
            "X": 14,
            "Y": 12
          }
        },
        {
          "X": 18,
          "Y": 9,
          "Width": 2,
          "Height": 6,
          "Left": 18,
          "Right": 20,
          "Top": 9,
          "Bottom": 15,
          "Area": 12,
          "Center": {
            "X": 19,
            "Y": 12
          }
        },
        {
          "X": 12,
          "Y": 14,
          "Width": 1,
          "Height": 2,
          "Left": 12,
          "Right": 13,
          "Top": 14,
          "Bottom": 16,
          "Area": 2,
          "Center": {
            "X": 12.5,
            "Y": 15
          }
        },
        {
          "X": 20,
          "Y": 14,
          "Width": 1,
          "Height": 2,
          "Left": 20,
          "Right": 21,
          "Top": 14,
          "Bottom": 16,
          "Area": 2,
          "Center": {
            "X": 20.5,
            "Y": 15
          }
        }
      ],
      "X": 12,
      "Y": 8,
      "Width": 9,
      "Height": 8
    },
    {
      "Id": "room-50",
      "Name": "\u041E\u0442\u0441\u0435\u043A 50",
      "Rects": [
        {
          "X": 15,
          "Y": 16,
          "Width": 3,
          "Height": 7,
          "Left": 15,
          "Right": 18,
          "Top": 16,
          "Bottom": 23,
          "Area": 21,
          "Center": {
            "X": 16.5,
            "Y": 19.5
          }
        },
        {
          "X": 14,
          "Y": 17,
          "Width": 1,
          "Height": 5,
          "Left": 14,
          "Right": 15,
          "Top": 17,
          "Bottom": 22,
          "Area": 5,
          "Center": {
            "X": 14.5,
            "Y": 19.5
          }
        },
        {
          "X": 18,
          "Y": 17,
          "Width": 1,
          "Height": 5,
          "Left": 18,
          "Right": 19,
          "Top": 17,
          "Bottom": 22,
          "Area": 5,
          "Center": {
            "X": 18.5,
            "Y": 19.5
          }
        },
        {
          "X": 12,
          "Y": 18,
          "Width": 2,
          "Height": 3,
          "Left": 12,
          "Right": 14,
          "Top": 18,
          "Bottom": 21,
          "Area": 6,
          "Center": {
            "X": 13,
            "Y": 19.5
          }
        },
        {
          "X": 19,
          "Y": 18,
          "Width": 2,
          "Height": 3,
          "Left": 19,
          "Right": 21,
          "Top": 18,
          "Bottom": 21,
          "Area": 6,
          "Center": {
            "X": 20,
            "Y": 19.5
          }
        }
      ],
      "X": 12,
      "Y": 16,
      "Width": 9,
      "Height": 7
    },
    {
      "Id": "room-55",
      "Name": "\u041E\u0442\u0441\u0435\u043A 55",
      "Rects": [
        {
          "X": 16,
          "Y": -2,
          "Width": 1,
          "Height": 10,
          "Left": 16,
          "Right": 17,
          "Top": -2,
          "Bottom": 8,
          "Area": 10,
          "Center": {
            "X": 16.5,
            "Y": 3
          }
        },
        {
          "X": 15,
          "Y": 0,
          "Width": 1,
          "Height": 8,
          "Left": 15,
          "Right": 16,
          "Top": 0,
          "Bottom": 8,
          "Area": 8,
          "Center": {
            "X": 15.5,
            "Y": 4
          }
        },
        {
          "X": 17,
          "Y": 0,
          "Width": 1,
          "Height": 8,
          "Left": 17,
          "Right": 18,
          "Top": 0,
          "Bottom": 8,
          "Area": 8,
          "Center": {
            "X": 17.5,
            "Y": 4
          }
        },
        {
          "X": 14,
          "Y": 3,
          "Width": 1,
          "Height": 4,
          "Left": 14,
          "Right": 15,
          "Top": 3,
          "Bottom": 7,
          "Area": 4,
          "Center": {
            "X": 14.5,
            "Y": 5
          }
        },
        {
          "X": 18,
          "Y": 3,
          "Width": 1,
          "Height": 4,
          "Left": 18,
          "Right": 19,
          "Top": 3,
          "Bottom": 7,
          "Area": 4,
          "Center": {
            "X": 18.5,
            "Y": 5
          }
        },
        {
          "X": 13,
          "Y": 6,
          "Width": 1,
          "Height": 2,
          "Left": 13,
          "Right": 14,
          "Top": 6,
          "Bottom": 8,
          "Area": 2,
          "Center": {
            "X": 13.5,
            "Y": 7
          }
        },
        {
          "X": 19,
          "Y": 6,
          "Width": 1,
          "Height": 2,
          "Left": 19,
          "Right": 20,
          "Top": 6,
          "Bottom": 8,
          "Area": 2,
          "Center": {
            "X": 19.5,
            "Y": 7
          }
        }
      ],
      "X": 13,
      "Y": -2,
      "Width": 7,
      "Height": 10
    },
    {
      "Id": "room-54",
      "Name": "\u041E\u0442\u0441\u0435\u043A 54",
      "Rects": [
        {
          "X": 10,
          "Y": 2,
          "Width": 2,
          "Height": 6,
          "Left": 10,
          "Right": 12,
          "Top": 2,
          "Bottom": 8,
          "Area": 12,
          "Center": {
            "X": 11,
            "Y": 5
          }
        },
        {
          "X": 12,
          "Y": 6,
          "Width": 1,
          "Height": 1,
          "Left": 12,
          "Right": 13,
          "Top": 6,
          "Bottom": 7,
          "Area": 1,
          "Center": {
            "X": 12.5,
            "Y": 6.5
          }
        }
      ],
      "X": 10,
      "Y": 2,
      "Width": 3,
      "Height": 6
    },
    {
      "Id": "room-56",
      "Name": "\u041E\u0442\u0441\u0435\u043A 56",
      "Rects": [
        {
          "X": 21,
          "Y": 2,
          "Width": 2,
          "Height": 6,
          "Left": 21,
          "Right": 23,
          "Top": 2,
          "Bottom": 8,
          "Area": 12,
          "Center": {
            "X": 22,
            "Y": 5
          }
        },
        {
          "X": 20,
          "Y": 6,
          "Width": 1,
          "Height": 1,
          "Left": 20,
          "Right": 21,
          "Top": 6,
          "Bottom": 7,
          "Area": 1,
          "Center": {
            "X": 20.5,
            "Y": 6.5
          }
        }
      ],
      "X": 20,
      "Y": 2,
      "Width": 3,
      "Height": 6
    },
    {
      "Id": "room-58",
      "Name": "\u041E\u0442\u0441\u0435\u043A 58",
      "Rects": [
        {
          "X": 7,
          "Y": 22,
          "Width": 3,
          "Height": 3,
          "Left": 7,
          "Right": 10,
          "Top": 22,
          "Bottom": 25,
          "Area": 9,
          "Center": {
            "X": 8.5,
            "Y": 23.5
          }
        },
        {
          "X": 4,
          "Y": 23,
          "Width": 3,
          "Height": 1,
          "Left": 4,
          "Right": 7,
          "Top": 23,
          "Bottom": 24,
          "Area": 3,
          "Center": {
            "X": 5.5,
            "Y": 23.5
          }
        }
      ],
      "X": 4,
      "Y": 22,
      "Width": 6,
      "Height": 3
    },
    {
      "Id": "room-60",
      "Name": "\u041E\u0442\u0441\u0435\u043A 60",
      "Rects": [
        {
          "X": 23,
          "Y": 22,
          "Width": 3,
          "Height": 3,
          "Left": 23,
          "Right": 26,
          "Top": 22,
          "Bottom": 25,
          "Area": 9,
          "Center": {
            "X": 24.5,
            "Y": 23.5
          }
        },
        {
          "X": 26,
          "Y": 23,
          "Width": 3,
          "Height": 1,
          "Left": 26,
          "Right": 29,
          "Top": 23,
          "Bottom": 24,
          "Area": 3,
          "Center": {
            "X": 27.5,
            "Y": 23.5
          }
        }
      ],
      "X": 23,
      "Y": 22,
      "Width": 6,
      "Height": 3
    },
    {
      "Id": "room-62",
      "Name": "\u041E\u0442\u0441\u0435\u043A 62",
      "Rects": [
        {
          "X": 29,
          "Y": 10,
          "Width": 4,
          "Height": 2,
          "Left": 29,
          "Right": 33,
          "Top": 10,
          "Bottom": 12,
          "Area": 8,
          "Center": {
            "X": 31,
            "Y": 11
          }
        },
        {
          "X": 31,
          "Y": 12,
          "Width": 1,
          "Height": 1,
          "Left": 31,
          "Right": 32,
          "Top": 12,
          "Bottom": 13,
          "Area": 1,
          "Center": {
            "X": 31.5,
            "Y": 12.5
          }
        }
      ],
      "X": 29,
      "Y": 10,
      "Width": 4,
      "Height": 3
    },
    {
      "Id": "room-64",
      "Name": "\u041E\u0442\u0441\u0435\u043A 64",
      "Rects": [
        {
          "X": 0,
          "Y": 13,
          "Width": 2,
          "Height": 6,
          "Left": 0,
          "Right": 2,
          "Top": 13,
          "Bottom": 19,
          "Area": 12,
          "Center": {
            "X": 1,
            "Y": 16
          }
        },
        {
          "X": 2,
          "Y": 17,
          "Width": 1,
          "Height": 1,
          "Left": 2,
          "Right": 3,
          "Top": 17,
          "Bottom": 18,
          "Area": 1,
          "Center": {
            "X": 2.5,
            "Y": 17.5
          }
        }
      ],
      "X": 0,
      "Y": 13,
      "Width": 3,
      "Height": 6
    }
  ],
  "Doors": [],
  "Airlocks": [
    {
      "RoomId": "room-63",
      "Side": "Left",
      "Id": null
    },
    {
      "RoomId": "room-62",
      "Side": "Right",
      "Id": null
    }
  ],
  "Devices": [
    {
      "Kind": "Jukebox",
      "X": 15.5,
      "Y": 0.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "Id": null
    },
    {
      "Kind": "CardTable",
      "X": 17.5,
      "Y": 0.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "Id": null
    },
    {
      "Kind": "Navigation",
      "X": 18,
      "Y": 4.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "Id": null
    },
    {
      "Kind": "Helm",
      "X": 15,
      "Y": 4.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "Id": null
    },
    {
      "Kind": "FuelRodStorage",
      "X": 19.5,
      "Y": 9.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "Id": null
    },
    {
      "Kind": "Oxygen",
      "X": 17.5,
      "Y": 13.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "Id": null
    },
    {
      "Kind": "Secondary",
      "X": 17.5,
      "Y": 10.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "Id": null
    },
    {
      "Kind": "Reactor",
      "X": 15,
      "Y": 12,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "Id": null
    },
    {
      "Kind": "WeaponPanel",
      "X": 9.5,
      "Y": 13.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "Id": null
    },
    {
      "Kind": "TurretBallistic",
      "X": 5.5,
      "Y": 15.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "Id": null
    },
    {
      "Kind": "Fabricator",
      "X": 23.5,
      "Y": 15.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "Id": null
    },
    {
      "Kind": "StorageRack",
      "X": 27.5,
      "Y": 18.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "Id": null
    },
    {
      "Kind": "StorageRack",
      "X": 26.5,
      "Y": 18.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "Id": null
    },
    {
      "Kind": "Camera",
      "X": 13.5,
      "Y": 20.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "Id": null
    },
    {
      "Kind": "Battery",
      "X": 14.5,
      "Y": 17.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "Id": null
    },
    {
      "Kind": "Battery",
      "X": 14.5,
      "Y": 21.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "Id": null
    },
    {
      "Kind": "Junction",
      "X": 15.5,
      "Y": 17.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "Id": null
    },
    {
      "Kind": "Junction",
      "X": 15.5,
      "Y": 21.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "Id": null
    },
    {
      "Kind": "Junction",
      "X": 16.5,
      "Y": 17.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "Id": null
    },
    {
      "Kind": "Junction",
      "X": 16.5,
      "Y": 21.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "Id": null
    },
    {
      "Kind": "Junction",
      "X": 17.5,
      "Y": 17.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "Id": null
    },
    {
      "Kind": "Junction",
      "X": 17.5,
      "Y": 21.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "Id": null
    },
    {
      "Kind": "Distribution",
      "X": 18.5,
      "Y": 19.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "Id": null
    },
    {
      "Kind": "WeaponPanel",
      "X": 5.5,
      "Y": 13.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "Id": null
    },
    {
      "Kind": "Camera",
      "X": 5.5,
      "Y": 18.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "Id": null
    },
    {
      "Kind": "ConstructionBench",
      "X": 25.5,
      "Y": 12,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "Id": null
    },
    {
      "Kind": "StorageRack",
      "X": 23.5,
      "Y": 18.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "Id": null
    },
    {
      "Kind": "Camera",
      "X": 24.5,
      "Y": 19.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "Id": null
    },
    {
      "Kind": "StorageRack",
      "X": 24.5,
      "Y": 18.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "Id": null
    },
    {
      "Kind": "Deconstructor",
      "X": 27.5,
      "Y": 15.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "Id": null
    },
    {
      "Kind": "SmallStorage",
      "X": 19.5,
      "Y": 14.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "Id": null
    },
    {
      "Kind": "TurretLaser",
      "X": 9.5,
      "Y": 15.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "Id": null
    },
    {
      "Kind": "SuitLocker",
      "X": 30.5,
      "Y": 11.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "Id": null
    },
    {
      "Kind": "SuitLocker",
      "X": 2.5,
      "Y": 11.5,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": false,
      "Id": null
    }
  ],
  "ForwardDegrees": -90,
  "WallMaterialsRaw": [],
  "EnginesRaw": [
    {
      "X": 21.5,
      "Y": 3.5,
      "Facing": "North",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 11.5,
      "Y": 3.5,
      "Facing": "North",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 31.5,
      "Y": 17.5,
      "Facing": "South",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 1.5,
      "Y": 17.5,
      "Facing": "South",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 5.5,
      "Y": 23.5,
      "Facing": "West",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 27.5,
      "Y": 23.5,
      "Facing": "East",
      "MaxThrust": 8,
      "Role": "Marching"
    }
  ],
  "SupplementalWallTilesRaw": [
    {
      "X": 30,
      "Y": 16
    },
    {
      "X": 30,
      "Y": 18
    },
    {
      "X": 30,
      "Y": 15
    },
    {
      "X": 30,
      "Y": 14
    },
    {
      "X": 30,
      "Y": 13
    },
    {
      "X": 29,
      "Y": 13
    },
    {
      "X": 29,
      "Y": 12
    },
    {
      "X": 28,
      "Y": 12
    },
    {
      "X": 2,
      "Y": 12
    },
    {
      "X": 0,
      "Y": 12
    },
    {
      "X": 3,
      "Y": 12
    },
    {
      "X": 3,
      "Y": 13
    },
    {
      "X": 4,
      "Y": 12
    },
    {
      "X": 21,
      "Y": 13
    },
    {
      "X": 21,
      "Y": 15
    },
    {
      "X": 22,
      "Y": 13
    },
    {
      "X": 21,
      "Y": 16
    },
    {
      "X": 23,
      "Y": 12
    },
    {
      "X": 22,
      "Y": 18
    },
    {
      "X": 21,
      "Y": 17
    },
    {
      "X": 23,
      "Y": 19
    },
    {
      "X": 23,
      "Y": 11
    },
    {
      "X": 27,
      "Y": 12
    },
    {
      "X": 28,
      "Y": 13
    },
    {
      "X": 29,
      "Y": 14
    },
    {
      "X": 29,
      "Y": 15
    },
    {
      "X": 27,
      "Y": 11
    },
    {
      "X": 23,
      "Y": 20
    },
    {
      "X": 29,
      "Y": 16
    },
    {
      "X": 27,
      "Y": 19
    },
    {
      "X": 28,
      "Y": 18
    },
    {
      "X": 26,
      "Y": 21
    },
    {
      "X": 27,
      "Y": 20
    },
    {
      "X": 22,
      "Y": 12
    },
    {
      "X": 22,
      "Y": 19
    },
    {
      "X": 21,
      "Y": 18
    },
    {
      "X": 20,
      "Y": 17
    },
    {
      "X": 23,
      "Y": 10
    },
    {
      "X": 27,
      "Y": 10
    },
    {
      "X": 28,
      "Y": 19
    },
    {
      "X": 27,
      "Y": 21
    },
    {
      "X": 3,
      "Y": 16
    },
    {
      "X": 4,
      "Y": 18
    },
    {
      "X": 3,
      "Y": 15
    },
    {
      "X": 5,
      "Y": 19
    },
    {
      "X": 4,
      "Y": 13
    },
    {
      "X": 3,
      "Y": 14
    },
    {
      "X": 5,
      "Y": 12
    },
    {
      "X": 6,
      "Y": 21
    },
    {
      "X": 5,
      "Y": 20
    },
    {
      "X": 9,
      "Y": 19
    },
    {
      "X": 10,
      "Y": 18
    },
    {
      "X": 11,
      "Y": 17
    },
    {
      "X": 11,
      "Y": 16
    },
    {
      "X": 9,
      "Y": 20
    },
    {
      "X": 6,
      "Y": 10
    },
    {
      "X": 5,
      "Y": 11
    },
    {
      "X": 11,
      "Y": 15
    },
    {
      "X": 7,
      "Y": 10
    },
    {
      "X": 9,
      "Y": 12
    },
    {
      "X": 10,
      "Y": 13
    },
    {
      "X": 9,
      "Y": 11
    },
    {
      "X": 11,
      "Y": 13
    },
    {
      "X": 4,
      "Y": 19
    },
    {
      "X": 5,
      "Y": 21
    },
    {
      "X": 10,
      "Y": 19
    },
    {
      "X": 11,
      "Y": 18
    },
    {
      "X": 12,
      "Y": 17
    },
    {
      "X": 5,
      "Y": 10
    },
    {
      "X": 10,
      "Y": 12
    },
    {
      "X": 9,
      "Y": 10
    },
    {
      "X": 12,
      "Y": 13
    },
    {
      "X": 13,
      "Y": 15
    },
    {
      "X": 14,
      "Y": 15
    },
    {
      "X": 12,
      "Y": 12
    },
    {
      "X": 12,
      "Y": 11
    },
    {
      "X": 12,
      "Y": 10
    },
    {
      "X": 13,
      "Y": 8
    },
    {
      "X": 12,
      "Y": 9
    },
    {
      "X": 18,
      "Y": 15
    },
    {
      "X": 14,
      "Y": 8
    },
    {
      "X": 19,
      "Y": 15
    },
    {
      "X": 20,
      "Y": 13
    },
    {
      "X": 20,
      "Y": 12
    },
    {
      "X": 20,
      "Y": 11
    },
    {
      "X": 18,
      "Y": 8
    },
    {
      "X": 20,
      "Y": 10
    },
    {
      "X": 19,
      "Y": 8
    },
    {
      "X": 20,
      "Y": 9
    },
    {
      "X": 13,
      "Y": 16
    },
    {
      "X": 12,
      "Y": 8
    },
    {
      "X": 19,
      "Y": 16
    },
    {
      "X": 20,
      "Y": 8
    },
    {
      "X": 14,
      "Y": 16
    },
    {
      "X": 13,
      "Y": 17
    },
    {
      "X": 18,
      "Y": 16
    },
    {
      "X": 19,
      "Y": 17
    },
    {
      "X": 14,
      "Y": 22
    },
    {
      "X": 13,
      "Y": 21
    },
    {
      "X": 18,
      "Y": 22
    },
    {
      "X": 19,
      "Y": 21
    },
    {
      "X": 13,
      "Y": 22
    },
    {
      "X": 12,
      "Y": 21
    },
    {
      "X": 19,
      "Y": 22
    },
    {
      "X": 20,
      "Y": 21
    },
    {
      "X": 14,
      "Y": 7
    },
    {
      "X": 18,
      "Y": 7
    },
    {
      "X": 13,
      "Y": 5
    },
    {
      "X": 13,
      "Y": 4
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
      "X": 19,
      "Y": 5
    },
    {
      "X": 14,
      "Y": 1
    },
    {
      "X": 19,
      "Y": 4
    },
    {
      "X": 15,
      "Y": -1
    },
    {
      "X": 14,
      "Y": 0
    },
    {
      "X": 18,
      "Y": 2
    },
    {
      "X": 19,
      "Y": 3
    },
    {
      "X": 18,
      "Y": 1
    },
    {
      "X": 17,
      "Y": -1
    },
    {
      "X": 18,
      "Y": 0
    },
    {
      "X": 13,
      "Y": 2
    },
    {
      "X": 15,
      "Y": -2
    },
    {
      "X": 14,
      "Y": -1
    },
    {
      "X": 19,
      "Y": 2
    },
    {
      "X": 17,
      "Y": -2
    },
    {
      "X": 18,
      "Y": -1
    },
    {
      "X": 12,
      "Y": 2
    },
    {
      "X": 20,
      "Y": 2
    },
    {
      "X": 12,
      "Y": 5
    },
    {
      "X": 12,
      "Y": 7
    },
    {
      "X": 12,
      "Y": 4
    },
    {
      "X": 12,
      "Y": 3
    },
    {
      "X": 20,
      "Y": 5
    },
    {
      "X": 20,
      "Y": 7
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
      "X": 6,
      "Y": 22
    },
    {
      "X": 6,
      "Y": 24
    },
    {
      "X": 5,
      "Y": 22
    },
    {
      "X": 5,
      "Y": 24
    },
    {
      "X": 4,
      "Y": 22
    },
    {
      "X": 4,
      "Y": 24
    },
    {
      "X": 26,
      "Y": 22
    },
    {
      "X": 26,
      "Y": 24
    },
    {
      "X": 27,
      "Y": 22
    },
    {
      "X": 27,
      "Y": 24
    },
    {
      "X": 28,
      "Y": 22
    },
    {
      "X": 28,
      "Y": 24
    },
    {
      "X": 32,
      "Y": 12
    },
    {
      "X": 30,
      "Y": 12
    },
    {
      "X": 2,
      "Y": 13
    },
    {
      "X": 2,
      "Y": 14
    },
    {
      "X": 2,
      "Y": 15
    },
    {
      "X": 2,
      "Y": 16
    },
    {
      "X": 2,
      "Y": 18
    }
  ],
  "ForcedFloorTilesRaw": [
    {
      "X": 30,
      "Y": 17
    },
    {
      "X": 31,
      "Y": 17
    },
    {
      "X": 31,
      "Y": 16
    },
    {
      "X": 31,
      "Y": 15
    },
    {
      "X": 31,
      "Y": 14
    },
    {
      "X": 31,
      "Y": 13
    },
    {
      "X": 1,
      "Y": 11
    },
    {
      "X": 1,
      "Y": 12
    },
    {
      "X": 2,
      "Y": 11
    },
    {
      "X": 21,
      "Y": 14
    },
    {
      "X": 22,
      "Y": 14
    },
    {
      "X": 22,
      "Y": 15
    },
    {
      "X": 23,
      "Y": 14
    },
    {
      "X": 22,
      "Y": 16
    },
    {
      "X": 23,
      "Y": 15
    },
    {
      "X": 23,
      "Y": 13
    },
    {
      "X": 24,
      "Y": 14
    },
    {
      "X": 22,
      "Y": 17
    },
    {
      "X": 23,
      "Y": 16
    },
    {
      "X": 24,
      "Y": 15
    },
    {
      "X": 24,
      "Y": 13
    },
    {
      "X": 25,
      "Y": 14
    },
    {
      "X": 23,
      "Y": 17
    },
    {
      "X": 24,
      "Y": 16
    },
    {
      "X": 25,
      "Y": 15
    },
    {
      "X": 24,
      "Y": 12
    },
    {
      "X": 25,
      "Y": 13
    },
    {
      "X": 26,
      "Y": 14
    },
    {
      "X": 23,
      "Y": 18
    },
    {
      "X": 24,
      "Y": 17
    },
    {
      "X": 25,
      "Y": 16
    },
    {
      "X": 26,
      "Y": 15
    },
    {
      "X": 24,
      "Y": 11
    },
    {
      "X": 25,
      "Y": 12
    },
    {
      "X": 26,
      "Y": 13
    },
    {
      "X": 27,
      "Y": 14
    },
    {
      "X": 24,
      "Y": 18
    },
    {
      "X": 25,
      "Y": 17
    },
    {
      "X": 26,
      "Y": 16
    },
    {
      "X": 27,
      "Y": 15
    },
    {
      "X": 25,
      "Y": 11
    },
    {
      "X": 26,
      "Y": 12
    },
    {
      "X": 27,
      "Y": 13
    },
    {
      "X": 28,
      "Y": 14
    },
    {
      "X": 24,
      "Y": 19
    },
    {
      "X": 25,
      "Y": 18
    },
    {
      "X": 26,
      "Y": 17
    },
    {
      "X": 27,
      "Y": 16
    },
    {
      "X": 28,
      "Y": 15
    },
    {
      "X": 26,
      "Y": 11
    },
    {
      "X": 24,
      "Y": 20
    },
    {
      "X": 25,
      "Y": 19
    },
    {
      "X": 26,
      "Y": 18
    },
    {
      "X": 27,
      "Y": 17
    },
    {
      "X": 28,
      "Y": 16
    },
    {
      "X": 24,
      "Y": 21
    },
    {
      "X": 25,
      "Y": 20
    },
    {
      "X": 26,
      "Y": 19
    },
    {
      "X": 27,
      "Y": 18
    },
    {
      "X": 28,
      "Y": 17
    },
    {
      "X": 25,
      "Y": 21
    },
    {
      "X": 26,
      "Y": 20
    },
    {
      "X": 29,
      "Y": 17
    },
    {
      "X": 8,
      "Y": 9
    },
    {
      "X": 3,
      "Y": 17
    },
    {
      "X": 4,
      "Y": 17
    },
    {
      "X": 4,
      "Y": 16
    },
    {
      "X": 5,
      "Y": 17
    },
    {
      "X": 4,
      "Y": 15
    },
    {
      "X": 5,
      "Y": 16
    },
    {
      "X": 5,
      "Y": 18
    },
    {
      "X": 6,
      "Y": 17
    },
    {
      "X": 4,
      "Y": 14
    },
    {
      "X": 5,
      "Y": 15
    },
    {
      "X": 6,
      "Y": 16
    },
    {
      "X": 6,
      "Y": 18
    },
    {
      "X": 7,
      "Y": 17
    },
    {
      "X": 5,
      "Y": 14
    },
    {
      "X": 6,
      "Y": 15
    },
    {
      "X": 7,
      "Y": 16
    },
    {
      "X": 6,
      "Y": 19
    },
    {
      "X": 7,
      "Y": 18
    },
    {
      "X": 8,
      "Y": 17
    },
    {
      "X": 5,
      "Y": 13
    },
    {
      "X": 6,
      "Y": 14
    },
    {
      "X": 7,
      "Y": 15
    },
    {
      "X": 8,
      "Y": 16
    },
    {
      "X": 6,
      "Y": 20
    },
    {
      "X": 7,
      "Y": 19
    },
    {
      "X": 8,
      "Y": 18
    },
    {
      "X": 9,
      "Y": 17
    },
    {
      "X": 6,
      "Y": 13
    },
    {
      "X": 7,
      "Y": 14
    },
    {
      "X": 8,
      "Y": 15
    },
    {
      "X": 9,
      "Y": 16
    },
    {
      "X": 7,
      "Y": 20
    },
    {
      "X": 8,
      "Y": 19
    },
    {
      "X": 9,
      "Y": 18
    },
    {
      "X": 10,
      "Y": 17
    },
    {
      "X": 6,
      "Y": 12
    },
    {
      "X": 7,
      "Y": 13
    },
    {
      "X": 8,
      "Y": 14
    },
    {
      "X": 9,
      "Y": 15
    },
    {
      "X": 10,
      "Y": 16
    },
    {
      "X": 7,
      "Y": 21
    },
    {
      "X": 8,
      "Y": 20
    },
    {
      "X": 6,
      "Y": 11
    },
    {
      "X": 7,
      "Y": 12
    },
    {
      "X": 8,
      "Y": 13
    },
    {
      "X": 9,
      "Y": 14
    },
    {
      "X": 10,
      "Y": 15
    },
    {
      "X": 8,
      "Y": 21
    },
    {
      "X": 7,
      "Y": 11
    },
    {
      "X": 8,
      "Y": 12
    },
    {
      "X": 9,
      "Y": 13
    },
    {
      "X": 10,
      "Y": 14
    },
    {
      "X": 8,
      "Y": 11
    },
    {
      "X": 11,
      "Y": 14
    },
    {
      "X": 12,
      "Y": 14
    },
    {
      "X": 13,
      "Y": 14
    },
    {
      "X": 13,
      "Y": 13
    },
    {
      "X": 14,
      "Y": 14
    },
    {
      "X": 13,
      "Y": 12
    },
    {
      "X": 14,
      "Y": 13
    },
    {
      "X": 15,
      "Y": 14
    },
    {
      "X": 13,
      "Y": 11
    },
    {
      "X": 14,
      "Y": 12
    },
    {
      "X": 15,
      "Y": 13
    },
    {
      "X": 15,
      "Y": 15
    },
    {
      "X": 16,
      "Y": 14
    },
    {
      "X": 13,
      "Y": 10
    },
    {
      "X": 14,
      "Y": 11
    },
    {
      "X": 15,
      "Y": 12
    },
    {
      "X": 16,
      "Y": 13
    },
    {
      "X": 16,
      "Y": 15
    },
    {
      "X": 17,
      "Y": 14
    },
    {
      "X": 13,
      "Y": 9
    },
    {
      "X": 14,
      "Y": 10
    },
    {
      "X": 15,
      "Y": 11
    },
    {
      "X": 16,
      "Y": 12
    },
    {
      "X": 17,
      "Y": 13
    },
    {
      "X": 17,
      "Y": 15
    },
    {
      "X": 18,
      "Y": 14
    },
    {
      "X": 14,
      "Y": 9
    },
    {
      "X": 15,
      "Y": 10
    },
    {
      "X": 16,
      "Y": 11
    },
    {
      "X": 17,
      "Y": 12
    },
    {
      "X": 18,
      "Y": 13
    },
    {
      "X": 19,
      "Y": 14
    },
    {
      "X": 15,
      "Y": 9
    },
    {
      "X": 16,
      "Y": 10
    },
    {
      "X": 17,
      "Y": 11
    },
    {
      "X": 18,
      "Y": 12
    },
    {
      "X": 19,
      "Y": 13
    },
    {
      "X": 20,
      "Y": 14
    },
    {
      "X": 15,
      "Y": 8
    },
    {
      "X": 16,
      "Y": 9
    },
    {
      "X": 17,
      "Y": 10
    },
    {
      "X": 18,
      "Y": 11
    },
    {
      "X": 19,
      "Y": 12
    },
    {
      "X": 16,
      "Y": 8
    },
    {
      "X": 17,
      "Y": 9
    },
    {
      "X": 18,
      "Y": 10
    },
    {
      "X": 19,
      "Y": 11
    },
    {
      "X": 17,
      "Y": 8
    },
    {
      "X": 18,
      "Y": 9
    },
    {
      "X": 19,
      "Y": 10
    },
    {
      "X": 19,
      "Y": 9
    },
    {
      "X": 15,
      "Y": 16
    },
    {
      "X": 15,
      "Y": 17
    },
    {
      "X": 16,
      "Y": 16
    },
    {
      "X": 15,
      "Y": 18
    },
    {
      "X": 16,
      "Y": 17
    },
    {
      "X": 14,
      "Y": 17
    },
    {
      "X": 17,
      "Y": 16
    },
    {
      "X": 15,
      "Y": 19
    },
    {
      "X": 16,
      "Y": 18
    },
    {
      "X": 14,
      "Y": 18
    },
    {
      "X": 17,
      "Y": 17
    },
    {
      "X": 15,
      "Y": 20
    },
    {
      "X": 16,
      "Y": 19
    },
    {
      "X": 14,
      "Y": 19
    },
    {
      "X": 17,
      "Y": 18
    },
    {
      "X": 13,
      "Y": 18
    },
    {
      "X": 18,
      "Y": 17
    },
    {
      "X": 15,
      "Y": 21
    },
    {
      "X": 16,
      "Y": 20
    },
    {
      "X": 14,
      "Y": 20
    },
    {
      "X": 17,
      "Y": 19
    },
    {
      "X": 13,
      "Y": 19
    },
    {
      "X": 18,
      "Y": 18
    },
    {
      "X": 16,
      "Y": 21
    },
    {
      "X": 14,
      "Y": 21
    },
    {
      "X": 17,
      "Y": 20
    },
    {
      "X": 13,
      "Y": 20
    },
    {
      "X": 18,
      "Y": 19
    },
    {
      "X": 19,
      "Y": 18
    },
    {
      "X": 17,
      "Y": 21
    },
    {
      "X": 18,
      "Y": 20
    },
    {
      "X": 19,
      "Y": 19
    },
    {
      "X": 18,
      "Y": 21
    },
    {
      "X": 19,
      "Y": 20
    },
    {
      "X": 15,
      "Y": 7
    },
    {
      "X": 15,
      "Y": 6
    },
    {
      "X": 16,
      "Y": 7
    },
    {
      "X": 15,
      "Y": 5
    },
    {
      "X": 16,
      "Y": 6
    },
    {
      "X": 14,
      "Y": 6
    },
    {
      "X": 17,
      "Y": 7
    },
    {
      "X": 15,
      "Y": 4
    },
    {
      "X": 16,
      "Y": 5
    },
    {
      "X": 14,
      "Y": 5
    },
    {
      "X": 17,
      "Y": 6
    },
    {
      "X": 13,
      "Y": 6
    },
    {
      "X": 15,
      "Y": 3
    },
    {
      "X": 16,
      "Y": 4
    },
    {
      "X": 14,
      "Y": 4
    },
    {
      "X": 17,
      "Y": 5
    },
    {
      "X": 18,
      "Y": 6
    },
    {
      "X": 15,
      "Y": 2
    },
    {
      "X": 16,
      "Y": 3
    },
    {
      "X": 14,
      "Y": 3
    },
    {
      "X": 17,
      "Y": 4
    },
    {
      "X": 18,
      "Y": 5
    },
    {
      "X": 19,
      "Y": 6
    },
    {
      "X": 15,
      "Y": 1
    },
    {
      "X": 16,
      "Y": 2
    },
    {
      "X": 17,
      "Y": 3
    },
    {
      "X": 18,
      "Y": 4
    },
    {
      "X": 15,
      "Y": 0
    },
    {
      "X": 16,
      "Y": 1
    },
    {
      "X": 17,
      "Y": 2
    },
    {
      "X": 18,
      "Y": 3
    },
    {
      "X": 16,
      "Y": 0
    },
    {
      "X": 17,
      "Y": 1
    },
    {
      "X": 16,
      "Y": -1
    },
    {
      "X": 17,
      "Y": 0
    },
    {
      "X": 12,
      "Y": 6
    },
    {
      "X": 11,
      "Y": 6
    },
    {
      "X": 11,
      "Y": 5
    },
    {
      "X": 11,
      "Y": 4
    },
    {
      "X": 11,
      "Y": 3
    },
    {
      "X": 20,
      "Y": 6
    },
    {
      "X": 21,
      "Y": 6
    },
    {
      "X": 21,
      "Y": 5
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
      "X": 7,
      "Y": 22
    },
    {
      "X": 7,
      "Y": 23
    },
    {
      "X": 8,
      "Y": 22
    },
    {
      "X": 8,
      "Y": 23
    },
    {
      "X": 6,
      "Y": 23
    },
    {
      "X": 5,
      "Y": 23
    },
    {
      "X": 24,
      "Y": 22
    },
    {
      "X": 24,
      "Y": 23
    },
    {
      "X": 25,
      "Y": 22
    },
    {
      "X": 25,
      "Y": 23
    },
    {
      "X": 26,
      "Y": 23
    },
    {
      "X": 27,
      "Y": 23
    },
    {
      "X": 31,
      "Y": 12
    },
    {
      "X": 31,
      "Y": 11
    },
    {
      "X": 30,
      "Y": 11
    },
    {
      "X": 1,
      "Y": 13
    },
    {
      "X": 1,
      "Y": 14
    },
    {
      "X": 1,
      "Y": 15
    },
    {
      "X": 1,
      "Y": 16
    },
    {
      "X": 1,
      "Y": 17
    },
    {
      "X": 2,
      "Y": 17
    }
  ],
  "WallOpenSidesRaw": [
    {
      "X": 16,
      "Y": -2,
      "Side": "North"
    },
    {
      "X": 14,
      "Y": 0,
      "Side": "West"
    },
    {
      "X": 14,
      "Y": 1,
      "Side": "West"
    },
    {
      "X": 14,
      "Y": 7,
      "Side": "South"
    },
    {
      "X": 18,
      "Y": 0,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": 1,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": 7,
      "Side": "South"
    },
    {
      "X": 13,
      "Y": 3,
      "Side": "West"
    },
    {
      "X": 13,
      "Y": 4,
      "Side": "West"
    },
    {
      "X": 13,
      "Y": 5,
      "Side": "West"
    },
    {
      "X": 19,
      "Y": 3,
      "Side": "East"
    },
    {
      "X": 19,
      "Y": 4,
      "Side": "East"
    },
    {
      "X": 19,
      "Y": 5,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": 15,
      "Side": "South"
    },
    {
      "X": 18,
      "Y": 8,
      "Side": "North"
    },
    {
      "X": 14,
      "Y": 15,
      "Side": "South"
    },
    {
      "X": 14,
      "Y": 8,
      "Side": "North"
    },
    {
      "X": 12,
      "Y": 13,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": 12,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": 11,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": 10,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": 9,
      "Side": "West"
    },
    {
      "X": 9,
      "Y": 11,
      "Side": "East"
    },
    {
      "X": 9,
      "Y": 20,
      "Side": "East"
    },
    {
      "X": 3,
      "Y": 14,
      "Side": "West"
    },
    {
      "X": 3,
      "Y": 15,
      "Side": "West"
    },
    {
      "X": 3,
      "Y": 16,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": 15,
      "Side": "East"
    },
    {
      "X": 11,
      "Y": 16,
      "Side": "East"
    },
    {
      "X": 11,
      "Y": 17,
      "Side": "East"
    },
    {
      "X": 29,
      "Y": 16,
      "Side": "East"
    },
    {
      "X": 29,
      "Y": 15,
      "Side": "East"
    },
    {
      "X": 29,
      "Y": 14,
      "Side": "East"
    },
    {
      "X": 21,
      "Y": 17,
      "Side": "West"
    },
    {
      "X": 21,
      "Y": 16,
      "Side": "West"
    },
    {
      "X": 21,
      "Y": 15,
      "Side": "West"
    },
    {
      "X": 27,
      "Y": 20,
      "Side": "East"
    },
    {
      "X": 27,
      "Y": 19,
      "Side": "East"
    },
    {
      "X": 27,
      "Y": 12,
      "Side": "East"
    },
    {
      "X": 27,
      "Y": 11,
      "Side": "East"
    },
    {
      "X": 26,
      "Y": 21,
      "Side": "South"
    },
    {
      "X": 12,
      "Y": 18,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": 19,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": 20,
      "Side": "West"
    },
    {
      "X": 20,
      "Y": 18,
      "Side": "East"
    },
    {
      "X": 20,
      "Y": 19,
      "Side": "East"
    },
    {
      "X": 20,
      "Y": 20,
      "Side": "East"
    },
    {
      "X": 14,
      "Y": 16,
      "Side": "North"
    },
    {
      "X": 18,
      "Y": 16,
      "Side": "North"
    },
    {
      "X": 14,
      "Y": 22,
      "Side": "South"
    },
    {
      "X": 15,
      "Y": 22,
      "Side": "South"
    },
    {
      "X": 16,
      "Y": 22,
      "Side": "South"
    },
    {
      "X": 17,
      "Y": 22,
      "Side": "South"
    },
    {
      "X": 18,
      "Y": 22,
      "Side": "South"
    },
    {
      "X": 5,
      "Y": 11,
      "Side": "West"
    },
    {
      "X": 5,
      "Y": 20,
      "Side": "West"
    },
    {
      "X": 23,
      "Y": 20,
      "Side": "West"
    },
    {
      "X": 23,
      "Y": 19,
      "Side": "West"
    },
    {
      "X": 23,
      "Y": 12,
      "Side": "West"
    },
    {
      "X": 23,
      "Y": 11,
      "Side": "West"
    },
    {
      "X": 20,
      "Y": 13,
      "Side": "East"
    },
    {
      "X": 20,
      "Y": 12,
      "Side": "East"
    },
    {
      "X": 20,
      "Y": 11,
      "Side": "East"
    },
    {
      "X": 20,
      "Y": 10,
      "Side": "East"
    },
    {
      "X": 20,
      "Y": 9,
      "Side": "East"
    },
    {
      "X": 20,
      "Y": 3,
      "Side": "West"
    },
    {
      "X": 20,
      "Y": 4,
      "Side": "West"
    },
    {
      "X": 20,
      "Y": 5,
      "Side": "West"
    },
    {
      "X": 21,
      "Y": 7,
      "Side": "South"
    },
    {
      "X": 22,
      "Y": 3,
      "Side": "East"
    },
    {
      "X": 22,
      "Y": 4,
      "Side": "East"
    },
    {
      "X": 22,
      "Y": 5,
      "Side": "East"
    },
    {
      "X": 22,
      "Y": 6,
      "Side": "East"
    },
    {
      "X": 0,
      "Y": 14,
      "Side": "West"
    },
    {
      "X": 0,
      "Y": 15,
      "Side": "West"
    },
    {
      "X": 0,
      "Y": 16,
      "Side": "West"
    },
    {
      "X": 0,
      "Y": 17,
      "Side": "West"
    },
    {
      "X": 2,
      "Y": 14,
      "Side": "East"
    },
    {
      "X": 2,
      "Y": 15,
      "Side": "East"
    },
    {
      "X": 2,
      "Y": 16,
      "Side": "East"
    },
    {
      "X": 30,
      "Y": 14,
      "Side": "West"
    },
    {
      "X": 30,
      "Y": 15,
      "Side": "West"
    },
    {
      "X": 30,
      "Y": 16,
      "Side": "West"
    },
    {
      "X": 32,
      "Y": 14,
      "Side": "East"
    },
    {
      "X": 32,
      "Y": 15,
      "Side": "East"
    },
    {
      "X": 32,
      "Y": 16,
      "Side": "East"
    },
    {
      "X": 32,
      "Y": 17,
      "Side": "East"
    },
    {
      "X": 10,
      "Y": 3,
      "Side": "West"
    },
    {
      "X": 10,
      "Y": 4,
      "Side": "West"
    },
    {
      "X": 10,
      "Y": 5,
      "Side": "West"
    },
    {
      "X": 10,
      "Y": 6,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": 7,
      "Side": "South"
    },
    {
      "X": 12,
      "Y": 3,
      "Side": "East"
    },
    {
      "X": 12,
      "Y": 4,
      "Side": "East"
    },
    {
      "X": 12,
      "Y": 5,
      "Side": "East"
    },
    {
      "X": 5,
      "Y": 22,
      "Side": "North"
    },
    {
      "X": 5,
      "Y": 24,
      "Side": "South"
    },
    {
      "X": 6,
      "Y": 22,
      "Side": "North"
    },
    {
      "X": 6,
      "Y": 24,
      "Side": "South"
    },
    {
      "X": 7,
      "Y": 24,
      "Side": "South"
    },
    {
      "X": 8,
      "Y": 24,
      "Side": "South"
    },
    {
      "X": 9,
      "Y": 23,
      "Side": "East"
    },
    {
      "X": 23,
      "Y": 23,
      "Side": "West"
    },
    {
      "X": 24,
      "Y": 24,
      "Side": "South"
    },
    {
      "X": 25,
      "Y": 24,
      "Side": "South"
    },
    {
      "X": 26,
      "Y": 22,
      "Side": "North"
    },
    {
      "X": 26,
      "Y": 24,
      "Side": "South"
    },
    {
      "X": 27,
      "Y": 22,
      "Side": "North"
    },
    {
      "X": 27,
      "Y": 24,
      "Side": "South"
    },
    {
      "X": 31,
      "Y": 10,
      "Side": "North"
    },
    {
      "X": 30,
      "Y": 12,
      "Side": "South"
    },
    {
      "X": 30,
      "Y": 10,
      "Side": "North"
    },
    {
      "X": 29,
      "Y": 11,
      "Side": "West"
    },
    {
      "X": 1,
      "Y": 10,
      "Side": "North"
    },
    {
      "X": 2,
      "Y": 10,
      "Side": "North"
    },
    {
      "X": 2,
      "Y": 12,
      "Side": "South"
    },
    {
      "X": 3,
      "Y": 11,
      "Side": "East"
    }
  ],
  "DoorEdgesRaw": [
    {
      "X": 2,
      "Y": 17,
      "Side": "East",
      "Id": "door-edge-0"
    },
    {
      "X": 29,
      "Y": 17,
      "Side": "East",
      "Id": "door-edge-1"
    },
    {
      "X": 20,
      "Y": 14,
      "Side": "East",
      "Id": "door-edge-2"
    },
    {
      "X": 11,
      "Y": 14,
      "Side": "East",
      "Id": "door-edge-4"
    },
    {
      "X": 15,
      "Y": 15,
      "Side": "South",
      "Id": "door-edge-5"
    },
    {
      "X": 16,
      "Y": 15,
      "Side": "South",
      "Id": "door-edge-5"
    },
    {
      "X": 17,
      "Y": 15,
      "Side": "South",
      "Id": "door-edge-5"
    },
    {
      "X": 15,
      "Y": 7,
      "Side": "South",
      "Id": "door-edge-6"
    },
    {
      "X": 16,
      "Y": 7,
      "Side": "South",
      "Id": "door-edge-6"
    },
    {
      "X": 17,
      "Y": 7,
      "Side": "South",
      "Id": "door-edge-6"
    },
    {
      "X": 12,
      "Y": 6,
      "Side": "East",
      "Id": "door-edge-7"
    },
    {
      "X": 19,
      "Y": 6,
      "Side": "East",
      "Id": "door-edge-8"
    },
    {
      "X": 7,
      "Y": 21,
      "Side": "South",
      "Id": "door-edge-9"
    },
    {
      "X": 8,
      "Y": 21,
      "Side": "South",
      "Id": "door-edge-9"
    },
    {
      "X": 24,
      "Y": 21,
      "Side": "South",
      "Id": "door-edge-10"
    },
    {
      "X": 25,
      "Y": 21,
      "Side": "South",
      "Id": "door-edge-10"
    },
    {
      "X": 31,
      "Y": 12,
      "Side": "South",
      "Id": "door-edge-11"
    },
    {
      "X": 1,
      "Y": 12,
      "Side": "South",
      "Id": "door-edge-13"
    }
  ],
  "WreckPatchesRaw": null,
  "WallMaterials": [],
  "Engines": [
    {
      "X": 21.5,
      "Y": 3.5,
      "Facing": "North",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 11.5,
      "Y": 3.5,
      "Facing": "North",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 31.5,
      "Y": 17.5,
      "Facing": "South",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 1.5,
      "Y": 17.5,
      "Facing": "South",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 5.5,
      "Y": 23.5,
      "Facing": "West",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 27.5,
      "Y": 23.5,
      "Facing": "East",
      "MaxThrust": 8,
      "Role": "Marching"
    }
  ],
  "SupplementalWallTiles": [
    {
      "X": 30,
      "Y": 16
    },
    {
      "X": 30,
      "Y": 18
    },
    {
      "X": 30,
      "Y": 15
    },
    {
      "X": 30,
      "Y": 14
    },
    {
      "X": 30,
      "Y": 13
    },
    {
      "X": 29,
      "Y": 13
    },
    {
      "X": 29,
      "Y": 12
    },
    {
      "X": 28,
      "Y": 12
    },
    {
      "X": 2,
      "Y": 12
    },
    {
      "X": 0,
      "Y": 12
    },
    {
      "X": 3,
      "Y": 12
    },
    {
      "X": 3,
      "Y": 13
    },
    {
      "X": 4,
      "Y": 12
    },
    {
      "X": 21,
      "Y": 13
    },
    {
      "X": 21,
      "Y": 15
    },
    {
      "X": 22,
      "Y": 13
    },
    {
      "X": 21,
      "Y": 16
    },
    {
      "X": 23,
      "Y": 12
    },
    {
      "X": 22,
      "Y": 18
    },
    {
      "X": 21,
      "Y": 17
    },
    {
      "X": 23,
      "Y": 19
    },
    {
      "X": 23,
      "Y": 11
    },
    {
      "X": 27,
      "Y": 12
    },
    {
      "X": 28,
      "Y": 13
    },
    {
      "X": 29,
      "Y": 14
    },
    {
      "X": 29,
      "Y": 15
    },
    {
      "X": 27,
      "Y": 11
    },
    {
      "X": 23,
      "Y": 20
    },
    {
      "X": 29,
      "Y": 16
    },
    {
      "X": 27,
      "Y": 19
    },
    {
      "X": 28,
      "Y": 18
    },
    {
      "X": 26,
      "Y": 21
    },
    {
      "X": 27,
      "Y": 20
    },
    {
      "X": 22,
      "Y": 12
    },
    {
      "X": 22,
      "Y": 19
    },
    {
      "X": 21,
      "Y": 18
    },
    {
      "X": 20,
      "Y": 17
    },
    {
      "X": 23,
      "Y": 10
    },
    {
      "X": 27,
      "Y": 10
    },
    {
      "X": 28,
      "Y": 19
    },
    {
      "X": 27,
      "Y": 21
    },
    {
      "X": 3,
      "Y": 16
    },
    {
      "X": 4,
      "Y": 18
    },
    {
      "X": 3,
      "Y": 15
    },
    {
      "X": 5,
      "Y": 19
    },
    {
      "X": 4,
      "Y": 13
    },
    {
      "X": 3,
      "Y": 14
    },
    {
      "X": 5,
      "Y": 12
    },
    {
      "X": 6,
      "Y": 21
    },
    {
      "X": 5,
      "Y": 20
    },
    {
      "X": 9,
      "Y": 19
    },
    {
      "X": 10,
      "Y": 18
    },
    {
      "X": 11,
      "Y": 17
    },
    {
      "X": 11,
      "Y": 16
    },
    {
      "X": 9,
      "Y": 20
    },
    {
      "X": 6,
      "Y": 10
    },
    {
      "X": 5,
      "Y": 11
    },
    {
      "X": 11,
      "Y": 15
    },
    {
      "X": 7,
      "Y": 10
    },
    {
      "X": 9,
      "Y": 12
    },
    {
      "X": 10,
      "Y": 13
    },
    {
      "X": 9,
      "Y": 11
    },
    {
      "X": 11,
      "Y": 13
    },
    {
      "X": 4,
      "Y": 19
    },
    {
      "X": 5,
      "Y": 21
    },
    {
      "X": 10,
      "Y": 19
    },
    {
      "X": 11,
      "Y": 18
    },
    {
      "X": 12,
      "Y": 17
    },
    {
      "X": 5,
      "Y": 10
    },
    {
      "X": 10,
      "Y": 12
    },
    {
      "X": 9,
      "Y": 10
    },
    {
      "X": 12,
      "Y": 13
    },
    {
      "X": 13,
      "Y": 15
    },
    {
      "X": 14,
      "Y": 15
    },
    {
      "X": 12,
      "Y": 12
    },
    {
      "X": 12,
      "Y": 11
    },
    {
      "X": 12,
      "Y": 10
    },
    {
      "X": 13,
      "Y": 8
    },
    {
      "X": 12,
      "Y": 9
    },
    {
      "X": 18,
      "Y": 15
    },
    {
      "X": 14,
      "Y": 8
    },
    {
      "X": 19,
      "Y": 15
    },
    {
      "X": 20,
      "Y": 13
    },
    {
      "X": 20,
      "Y": 12
    },
    {
      "X": 20,
      "Y": 11
    },
    {
      "X": 18,
      "Y": 8
    },
    {
      "X": 20,
      "Y": 10
    },
    {
      "X": 19,
      "Y": 8
    },
    {
      "X": 20,
      "Y": 9
    },
    {
      "X": 13,
      "Y": 16
    },
    {
      "X": 12,
      "Y": 8
    },
    {
      "X": 19,
      "Y": 16
    },
    {
      "X": 20,
      "Y": 8
    },
    {
      "X": 14,
      "Y": 16
    },
    {
      "X": 13,
      "Y": 17
    },
    {
      "X": 18,
      "Y": 16
    },
    {
      "X": 19,
      "Y": 17
    },
    {
      "X": 14,
      "Y": 22
    },
    {
      "X": 13,
      "Y": 21
    },
    {
      "X": 18,
      "Y": 22
    },
    {
      "X": 19,
      "Y": 21
    },
    {
      "X": 13,
      "Y": 22
    },
    {
      "X": 12,
      "Y": 21
    },
    {
      "X": 19,
      "Y": 22
    },
    {
      "X": 20,
      "Y": 21
    },
    {
      "X": 14,
      "Y": 7
    },
    {
      "X": 18,
      "Y": 7
    },
    {
      "X": 13,
      "Y": 5
    },
    {
      "X": 13,
      "Y": 4
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
      "X": 19,
      "Y": 5
    },
    {
      "X": 14,
      "Y": 1
    },
    {
      "X": 19,
      "Y": 4
    },
    {
      "X": 15,
      "Y": -1
    },
    {
      "X": 14,
      "Y": 0
    },
    {
      "X": 18,
      "Y": 2
    },
    {
      "X": 19,
      "Y": 3
    },
    {
      "X": 18,
      "Y": 1
    },
    {
      "X": 17,
      "Y": -1
    },
    {
      "X": 18,
      "Y": 0
    },
    {
      "X": 13,
      "Y": 2
    },
    {
      "X": 15,
      "Y": -2
    },
    {
      "X": 14,
      "Y": -1
    },
    {
      "X": 19,
      "Y": 2
    },
    {
      "X": 17,
      "Y": -2
    },
    {
      "X": 18,
      "Y": -1
    },
    {
      "X": 12,
      "Y": 2
    },
    {
      "X": 20,
      "Y": 2
    },
    {
      "X": 12,
      "Y": 5
    },
    {
      "X": 12,
      "Y": 7
    },
    {
      "X": 12,
      "Y": 4
    },
    {
      "X": 12,
      "Y": 3
    },
    {
      "X": 20,
      "Y": 5
    },
    {
      "X": 20,
      "Y": 7
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
      "X": 6,
      "Y": 22
    },
    {
      "X": 6,
      "Y": 24
    },
    {
      "X": 5,
      "Y": 22
    },
    {
      "X": 5,
      "Y": 24
    },
    {
      "X": 4,
      "Y": 22
    },
    {
      "X": 4,
      "Y": 24
    },
    {
      "X": 26,
      "Y": 22
    },
    {
      "X": 26,
      "Y": 24
    },
    {
      "X": 27,
      "Y": 22
    },
    {
      "X": 27,
      "Y": 24
    },
    {
      "X": 28,
      "Y": 22
    },
    {
      "X": 28,
      "Y": 24
    },
    {
      "X": 32,
      "Y": 12
    },
    {
      "X": 30,
      "Y": 12
    },
    {
      "X": 2,
      "Y": 13
    },
    {
      "X": 2,
      "Y": 14
    },
    {
      "X": 2,
      "Y": 15
    },
    {
      "X": 2,
      "Y": 16
    },
    {
      "X": 2,
      "Y": 18
    }
  ],
  "ForcedFloorTiles": [
    {
      "X": 30,
      "Y": 17
    },
    {
      "X": 31,
      "Y": 17
    },
    {
      "X": 31,
      "Y": 16
    },
    {
      "X": 31,
      "Y": 15
    },
    {
      "X": 31,
      "Y": 14
    },
    {
      "X": 31,
      "Y": 13
    },
    {
      "X": 1,
      "Y": 11
    },
    {
      "X": 1,
      "Y": 12
    },
    {
      "X": 2,
      "Y": 11
    },
    {
      "X": 21,
      "Y": 14
    },
    {
      "X": 22,
      "Y": 14
    },
    {
      "X": 22,
      "Y": 15
    },
    {
      "X": 23,
      "Y": 14
    },
    {
      "X": 22,
      "Y": 16
    },
    {
      "X": 23,
      "Y": 15
    },
    {
      "X": 23,
      "Y": 13
    },
    {
      "X": 24,
      "Y": 14
    },
    {
      "X": 22,
      "Y": 17
    },
    {
      "X": 23,
      "Y": 16
    },
    {
      "X": 24,
      "Y": 15
    },
    {
      "X": 24,
      "Y": 13
    },
    {
      "X": 25,
      "Y": 14
    },
    {
      "X": 23,
      "Y": 17
    },
    {
      "X": 24,
      "Y": 16
    },
    {
      "X": 25,
      "Y": 15
    },
    {
      "X": 24,
      "Y": 12
    },
    {
      "X": 25,
      "Y": 13
    },
    {
      "X": 26,
      "Y": 14
    },
    {
      "X": 23,
      "Y": 18
    },
    {
      "X": 24,
      "Y": 17
    },
    {
      "X": 25,
      "Y": 16
    },
    {
      "X": 26,
      "Y": 15
    },
    {
      "X": 24,
      "Y": 11
    },
    {
      "X": 25,
      "Y": 12
    },
    {
      "X": 26,
      "Y": 13
    },
    {
      "X": 27,
      "Y": 14
    },
    {
      "X": 24,
      "Y": 18
    },
    {
      "X": 25,
      "Y": 17
    },
    {
      "X": 26,
      "Y": 16
    },
    {
      "X": 27,
      "Y": 15
    },
    {
      "X": 25,
      "Y": 11
    },
    {
      "X": 26,
      "Y": 12
    },
    {
      "X": 27,
      "Y": 13
    },
    {
      "X": 28,
      "Y": 14
    },
    {
      "X": 24,
      "Y": 19
    },
    {
      "X": 25,
      "Y": 18
    },
    {
      "X": 26,
      "Y": 17
    },
    {
      "X": 27,
      "Y": 16
    },
    {
      "X": 28,
      "Y": 15
    },
    {
      "X": 26,
      "Y": 11
    },
    {
      "X": 24,
      "Y": 20
    },
    {
      "X": 25,
      "Y": 19
    },
    {
      "X": 26,
      "Y": 18
    },
    {
      "X": 27,
      "Y": 17
    },
    {
      "X": 28,
      "Y": 16
    },
    {
      "X": 24,
      "Y": 21
    },
    {
      "X": 25,
      "Y": 20
    },
    {
      "X": 26,
      "Y": 19
    },
    {
      "X": 27,
      "Y": 18
    },
    {
      "X": 28,
      "Y": 17
    },
    {
      "X": 25,
      "Y": 21
    },
    {
      "X": 26,
      "Y": 20
    },
    {
      "X": 29,
      "Y": 17
    },
    {
      "X": 8,
      "Y": 9
    },
    {
      "X": 3,
      "Y": 17
    },
    {
      "X": 4,
      "Y": 17
    },
    {
      "X": 4,
      "Y": 16
    },
    {
      "X": 5,
      "Y": 17
    },
    {
      "X": 4,
      "Y": 15
    },
    {
      "X": 5,
      "Y": 16
    },
    {
      "X": 5,
      "Y": 18
    },
    {
      "X": 6,
      "Y": 17
    },
    {
      "X": 4,
      "Y": 14
    },
    {
      "X": 5,
      "Y": 15
    },
    {
      "X": 6,
      "Y": 16
    },
    {
      "X": 6,
      "Y": 18
    },
    {
      "X": 7,
      "Y": 17
    },
    {
      "X": 5,
      "Y": 14
    },
    {
      "X": 6,
      "Y": 15
    },
    {
      "X": 7,
      "Y": 16
    },
    {
      "X": 6,
      "Y": 19
    },
    {
      "X": 7,
      "Y": 18
    },
    {
      "X": 8,
      "Y": 17
    },
    {
      "X": 5,
      "Y": 13
    },
    {
      "X": 6,
      "Y": 14
    },
    {
      "X": 7,
      "Y": 15
    },
    {
      "X": 8,
      "Y": 16
    },
    {
      "X": 6,
      "Y": 20
    },
    {
      "X": 7,
      "Y": 19
    },
    {
      "X": 8,
      "Y": 18
    },
    {
      "X": 9,
      "Y": 17
    },
    {
      "X": 6,
      "Y": 13
    },
    {
      "X": 7,
      "Y": 14
    },
    {
      "X": 8,
      "Y": 15
    },
    {
      "X": 9,
      "Y": 16
    },
    {
      "X": 7,
      "Y": 20
    },
    {
      "X": 8,
      "Y": 19
    },
    {
      "X": 9,
      "Y": 18
    },
    {
      "X": 10,
      "Y": 17
    },
    {
      "X": 6,
      "Y": 12
    },
    {
      "X": 7,
      "Y": 13
    },
    {
      "X": 8,
      "Y": 14
    },
    {
      "X": 9,
      "Y": 15
    },
    {
      "X": 10,
      "Y": 16
    },
    {
      "X": 7,
      "Y": 21
    },
    {
      "X": 8,
      "Y": 20
    },
    {
      "X": 6,
      "Y": 11
    },
    {
      "X": 7,
      "Y": 12
    },
    {
      "X": 8,
      "Y": 13
    },
    {
      "X": 9,
      "Y": 14
    },
    {
      "X": 10,
      "Y": 15
    },
    {
      "X": 8,
      "Y": 21
    },
    {
      "X": 7,
      "Y": 11
    },
    {
      "X": 8,
      "Y": 12
    },
    {
      "X": 9,
      "Y": 13
    },
    {
      "X": 10,
      "Y": 14
    },
    {
      "X": 8,
      "Y": 11
    },
    {
      "X": 11,
      "Y": 14
    },
    {
      "X": 12,
      "Y": 14
    },
    {
      "X": 13,
      "Y": 14
    },
    {
      "X": 13,
      "Y": 13
    },
    {
      "X": 14,
      "Y": 14
    },
    {
      "X": 13,
      "Y": 12
    },
    {
      "X": 14,
      "Y": 13
    },
    {
      "X": 15,
      "Y": 14
    },
    {
      "X": 13,
      "Y": 11
    },
    {
      "X": 14,
      "Y": 12
    },
    {
      "X": 15,
      "Y": 13
    },
    {
      "X": 15,
      "Y": 15
    },
    {
      "X": 16,
      "Y": 14
    },
    {
      "X": 13,
      "Y": 10
    },
    {
      "X": 14,
      "Y": 11
    },
    {
      "X": 15,
      "Y": 12
    },
    {
      "X": 16,
      "Y": 13
    },
    {
      "X": 16,
      "Y": 15
    },
    {
      "X": 17,
      "Y": 14
    },
    {
      "X": 13,
      "Y": 9
    },
    {
      "X": 14,
      "Y": 10
    },
    {
      "X": 15,
      "Y": 11
    },
    {
      "X": 16,
      "Y": 12
    },
    {
      "X": 17,
      "Y": 13
    },
    {
      "X": 17,
      "Y": 15
    },
    {
      "X": 18,
      "Y": 14
    },
    {
      "X": 14,
      "Y": 9
    },
    {
      "X": 15,
      "Y": 10
    },
    {
      "X": 16,
      "Y": 11
    },
    {
      "X": 17,
      "Y": 12
    },
    {
      "X": 18,
      "Y": 13
    },
    {
      "X": 19,
      "Y": 14
    },
    {
      "X": 15,
      "Y": 9
    },
    {
      "X": 16,
      "Y": 10
    },
    {
      "X": 17,
      "Y": 11
    },
    {
      "X": 18,
      "Y": 12
    },
    {
      "X": 19,
      "Y": 13
    },
    {
      "X": 20,
      "Y": 14
    },
    {
      "X": 15,
      "Y": 8
    },
    {
      "X": 16,
      "Y": 9
    },
    {
      "X": 17,
      "Y": 10
    },
    {
      "X": 18,
      "Y": 11
    },
    {
      "X": 19,
      "Y": 12
    },
    {
      "X": 16,
      "Y": 8
    },
    {
      "X": 17,
      "Y": 9
    },
    {
      "X": 18,
      "Y": 10
    },
    {
      "X": 19,
      "Y": 11
    },
    {
      "X": 17,
      "Y": 8
    },
    {
      "X": 18,
      "Y": 9
    },
    {
      "X": 19,
      "Y": 10
    },
    {
      "X": 19,
      "Y": 9
    },
    {
      "X": 15,
      "Y": 16
    },
    {
      "X": 15,
      "Y": 17
    },
    {
      "X": 16,
      "Y": 16
    },
    {
      "X": 15,
      "Y": 18
    },
    {
      "X": 16,
      "Y": 17
    },
    {
      "X": 14,
      "Y": 17
    },
    {
      "X": 17,
      "Y": 16
    },
    {
      "X": 15,
      "Y": 19
    },
    {
      "X": 16,
      "Y": 18
    },
    {
      "X": 14,
      "Y": 18
    },
    {
      "X": 17,
      "Y": 17
    },
    {
      "X": 15,
      "Y": 20
    },
    {
      "X": 16,
      "Y": 19
    },
    {
      "X": 14,
      "Y": 19
    },
    {
      "X": 17,
      "Y": 18
    },
    {
      "X": 13,
      "Y": 18
    },
    {
      "X": 18,
      "Y": 17
    },
    {
      "X": 15,
      "Y": 21
    },
    {
      "X": 16,
      "Y": 20
    },
    {
      "X": 14,
      "Y": 20
    },
    {
      "X": 17,
      "Y": 19
    },
    {
      "X": 13,
      "Y": 19
    },
    {
      "X": 18,
      "Y": 18
    },
    {
      "X": 16,
      "Y": 21
    },
    {
      "X": 14,
      "Y": 21
    },
    {
      "X": 17,
      "Y": 20
    },
    {
      "X": 13,
      "Y": 20
    },
    {
      "X": 18,
      "Y": 19
    },
    {
      "X": 19,
      "Y": 18
    },
    {
      "X": 17,
      "Y": 21
    },
    {
      "X": 18,
      "Y": 20
    },
    {
      "X": 19,
      "Y": 19
    },
    {
      "X": 18,
      "Y": 21
    },
    {
      "X": 19,
      "Y": 20
    },
    {
      "X": 15,
      "Y": 7
    },
    {
      "X": 15,
      "Y": 6
    },
    {
      "X": 16,
      "Y": 7
    },
    {
      "X": 15,
      "Y": 5
    },
    {
      "X": 16,
      "Y": 6
    },
    {
      "X": 14,
      "Y": 6
    },
    {
      "X": 17,
      "Y": 7
    },
    {
      "X": 15,
      "Y": 4
    },
    {
      "X": 16,
      "Y": 5
    },
    {
      "X": 14,
      "Y": 5
    },
    {
      "X": 17,
      "Y": 6
    },
    {
      "X": 13,
      "Y": 6
    },
    {
      "X": 15,
      "Y": 3
    },
    {
      "X": 16,
      "Y": 4
    },
    {
      "X": 14,
      "Y": 4
    },
    {
      "X": 17,
      "Y": 5
    },
    {
      "X": 18,
      "Y": 6
    },
    {
      "X": 15,
      "Y": 2
    },
    {
      "X": 16,
      "Y": 3
    },
    {
      "X": 14,
      "Y": 3
    },
    {
      "X": 17,
      "Y": 4
    },
    {
      "X": 18,
      "Y": 5
    },
    {
      "X": 19,
      "Y": 6
    },
    {
      "X": 15,
      "Y": 1
    },
    {
      "X": 16,
      "Y": 2
    },
    {
      "X": 17,
      "Y": 3
    },
    {
      "X": 18,
      "Y": 4
    },
    {
      "X": 15,
      "Y": 0
    },
    {
      "X": 16,
      "Y": 1
    },
    {
      "X": 17,
      "Y": 2
    },
    {
      "X": 18,
      "Y": 3
    },
    {
      "X": 16,
      "Y": 0
    },
    {
      "X": 17,
      "Y": 1
    },
    {
      "X": 16,
      "Y": -1
    },
    {
      "X": 17,
      "Y": 0
    },
    {
      "X": 12,
      "Y": 6
    },
    {
      "X": 11,
      "Y": 6
    },
    {
      "X": 11,
      "Y": 5
    },
    {
      "X": 11,
      "Y": 4
    },
    {
      "X": 11,
      "Y": 3
    },
    {
      "X": 20,
      "Y": 6
    },
    {
      "X": 21,
      "Y": 6
    },
    {
      "X": 21,
      "Y": 5
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
      "X": 7,
      "Y": 22
    },
    {
      "X": 7,
      "Y": 23
    },
    {
      "X": 8,
      "Y": 22
    },
    {
      "X": 8,
      "Y": 23
    },
    {
      "X": 6,
      "Y": 23
    },
    {
      "X": 5,
      "Y": 23
    },
    {
      "X": 24,
      "Y": 22
    },
    {
      "X": 24,
      "Y": 23
    },
    {
      "X": 25,
      "Y": 22
    },
    {
      "X": 25,
      "Y": 23
    },
    {
      "X": 26,
      "Y": 23
    },
    {
      "X": 27,
      "Y": 23
    },
    {
      "X": 31,
      "Y": 12
    },
    {
      "X": 31,
      "Y": 11
    },
    {
      "X": 30,
      "Y": 11
    },
    {
      "X": 1,
      "Y": 13
    },
    {
      "X": 1,
      "Y": 14
    },
    {
      "X": 1,
      "Y": 15
    },
    {
      "X": 1,
      "Y": 16
    },
    {
      "X": 1,
      "Y": 17
    },
    {
      "X": 2,
      "Y": 17
    }
  ],
  "WallOpenSides": [
    {
      "X": 16,
      "Y": -2,
      "Side": "North"
    },
    {
      "X": 14,
      "Y": 0,
      "Side": "West"
    },
    {
      "X": 14,
      "Y": 1,
      "Side": "West"
    },
    {
      "X": 14,
      "Y": 7,
      "Side": "South"
    },
    {
      "X": 18,
      "Y": 0,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": 1,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": 7,
      "Side": "South"
    },
    {
      "X": 13,
      "Y": 3,
      "Side": "West"
    },
    {
      "X": 13,
      "Y": 4,
      "Side": "West"
    },
    {
      "X": 13,
      "Y": 5,
      "Side": "West"
    },
    {
      "X": 19,
      "Y": 3,
      "Side": "East"
    },
    {
      "X": 19,
      "Y": 4,
      "Side": "East"
    },
    {
      "X": 19,
      "Y": 5,
      "Side": "East"
    },
    {
      "X": 18,
      "Y": 15,
      "Side": "South"
    },
    {
      "X": 18,
      "Y": 8,
      "Side": "North"
    },
    {
      "X": 14,
      "Y": 15,
      "Side": "South"
    },
    {
      "X": 14,
      "Y": 8,
      "Side": "North"
    },
    {
      "X": 12,
      "Y": 13,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": 12,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": 11,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": 10,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": 9,
      "Side": "West"
    },
    {
      "X": 9,
      "Y": 11,
      "Side": "East"
    },
    {
      "X": 9,
      "Y": 20,
      "Side": "East"
    },
    {
      "X": 3,
      "Y": 14,
      "Side": "West"
    },
    {
      "X": 3,
      "Y": 15,
      "Side": "West"
    },
    {
      "X": 3,
      "Y": 16,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": 15,
      "Side": "East"
    },
    {
      "X": 11,
      "Y": 16,
      "Side": "East"
    },
    {
      "X": 11,
      "Y": 17,
      "Side": "East"
    },
    {
      "X": 29,
      "Y": 16,
      "Side": "East"
    },
    {
      "X": 29,
      "Y": 15,
      "Side": "East"
    },
    {
      "X": 29,
      "Y": 14,
      "Side": "East"
    },
    {
      "X": 21,
      "Y": 17,
      "Side": "West"
    },
    {
      "X": 21,
      "Y": 16,
      "Side": "West"
    },
    {
      "X": 21,
      "Y": 15,
      "Side": "West"
    },
    {
      "X": 27,
      "Y": 20,
      "Side": "East"
    },
    {
      "X": 27,
      "Y": 19,
      "Side": "East"
    },
    {
      "X": 27,
      "Y": 12,
      "Side": "East"
    },
    {
      "X": 27,
      "Y": 11,
      "Side": "East"
    },
    {
      "X": 26,
      "Y": 21,
      "Side": "South"
    },
    {
      "X": 12,
      "Y": 18,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": 19,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": 20,
      "Side": "West"
    },
    {
      "X": 20,
      "Y": 18,
      "Side": "East"
    },
    {
      "X": 20,
      "Y": 19,
      "Side": "East"
    },
    {
      "X": 20,
      "Y": 20,
      "Side": "East"
    },
    {
      "X": 14,
      "Y": 16,
      "Side": "North"
    },
    {
      "X": 18,
      "Y": 16,
      "Side": "North"
    },
    {
      "X": 14,
      "Y": 22,
      "Side": "South"
    },
    {
      "X": 15,
      "Y": 22,
      "Side": "South"
    },
    {
      "X": 16,
      "Y": 22,
      "Side": "South"
    },
    {
      "X": 17,
      "Y": 22,
      "Side": "South"
    },
    {
      "X": 18,
      "Y": 22,
      "Side": "South"
    },
    {
      "X": 5,
      "Y": 11,
      "Side": "West"
    },
    {
      "X": 5,
      "Y": 20,
      "Side": "West"
    },
    {
      "X": 23,
      "Y": 20,
      "Side": "West"
    },
    {
      "X": 23,
      "Y": 19,
      "Side": "West"
    },
    {
      "X": 23,
      "Y": 12,
      "Side": "West"
    },
    {
      "X": 23,
      "Y": 11,
      "Side": "West"
    },
    {
      "X": 20,
      "Y": 13,
      "Side": "East"
    },
    {
      "X": 20,
      "Y": 12,
      "Side": "East"
    },
    {
      "X": 20,
      "Y": 11,
      "Side": "East"
    },
    {
      "X": 20,
      "Y": 10,
      "Side": "East"
    },
    {
      "X": 20,
      "Y": 9,
      "Side": "East"
    },
    {
      "X": 20,
      "Y": 3,
      "Side": "West"
    },
    {
      "X": 20,
      "Y": 4,
      "Side": "West"
    },
    {
      "X": 20,
      "Y": 5,
      "Side": "West"
    },
    {
      "X": 21,
      "Y": 7,
      "Side": "South"
    },
    {
      "X": 22,
      "Y": 3,
      "Side": "East"
    },
    {
      "X": 22,
      "Y": 4,
      "Side": "East"
    },
    {
      "X": 22,
      "Y": 5,
      "Side": "East"
    },
    {
      "X": 22,
      "Y": 6,
      "Side": "East"
    },
    {
      "X": 0,
      "Y": 14,
      "Side": "West"
    },
    {
      "X": 0,
      "Y": 15,
      "Side": "West"
    },
    {
      "X": 0,
      "Y": 16,
      "Side": "West"
    },
    {
      "X": 0,
      "Y": 17,
      "Side": "West"
    },
    {
      "X": 2,
      "Y": 14,
      "Side": "East"
    },
    {
      "X": 2,
      "Y": 15,
      "Side": "East"
    },
    {
      "X": 2,
      "Y": 16,
      "Side": "East"
    },
    {
      "X": 30,
      "Y": 14,
      "Side": "West"
    },
    {
      "X": 30,
      "Y": 15,
      "Side": "West"
    },
    {
      "X": 30,
      "Y": 16,
      "Side": "West"
    },
    {
      "X": 32,
      "Y": 14,
      "Side": "East"
    },
    {
      "X": 32,
      "Y": 15,
      "Side": "East"
    },
    {
      "X": 32,
      "Y": 16,
      "Side": "East"
    },
    {
      "X": 32,
      "Y": 17,
      "Side": "East"
    },
    {
      "X": 10,
      "Y": 3,
      "Side": "West"
    },
    {
      "X": 10,
      "Y": 4,
      "Side": "West"
    },
    {
      "X": 10,
      "Y": 5,
      "Side": "West"
    },
    {
      "X": 10,
      "Y": 6,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": 7,
      "Side": "South"
    },
    {
      "X": 12,
      "Y": 3,
      "Side": "East"
    },
    {
      "X": 12,
      "Y": 4,
      "Side": "East"
    },
    {
      "X": 12,
      "Y": 5,
      "Side": "East"
    },
    {
      "X": 5,
      "Y": 22,
      "Side": "North"
    },
    {
      "X": 5,
      "Y": 24,
      "Side": "South"
    },
    {
      "X": 6,
      "Y": 22,
      "Side": "North"
    },
    {
      "X": 6,
      "Y": 24,
      "Side": "South"
    },
    {
      "X": 7,
      "Y": 24,
      "Side": "South"
    },
    {
      "X": 8,
      "Y": 24,
      "Side": "South"
    },
    {
      "X": 9,
      "Y": 23,
      "Side": "East"
    },
    {
      "X": 23,
      "Y": 23,
      "Side": "West"
    },
    {
      "X": 24,
      "Y": 24,
      "Side": "South"
    },
    {
      "X": 25,
      "Y": 24,
      "Side": "South"
    },
    {
      "X": 26,
      "Y": 22,
      "Side": "North"
    },
    {
      "X": 26,
      "Y": 24,
      "Side": "South"
    },
    {
      "X": 27,
      "Y": 22,
      "Side": "North"
    },
    {
      "X": 27,
      "Y": 24,
      "Side": "South"
    },
    {
      "X": 31,
      "Y": 10,
      "Side": "North"
    },
    {
      "X": 30,
      "Y": 12,
      "Side": "South"
    },
    {
      "X": 30,
      "Y": 10,
      "Side": "North"
    },
    {
      "X": 29,
      "Y": 11,
      "Side": "West"
    },
    {
      "X": 1,
      "Y": 10,
      "Side": "North"
    },
    {
      "X": 2,
      "Y": 10,
      "Side": "North"
    },
    {
      "X": 2,
      "Y": 12,
      "Side": "South"
    },
    {
      "X": 3,
      "Y": 11,
      "Side": "East"
    }
  ],
  "DoorEdges": [
    {
      "X": 2,
      "Y": 17,
      "Side": "East",
      "Id": "door-edge-0"
    },
    {
      "X": 29,
      "Y": 17,
      "Side": "East",
      "Id": "door-edge-1"
    },
    {
      "X": 20,
      "Y": 14,
      "Side": "East",
      "Id": "door-edge-2"
    },
    {
      "X": 11,
      "Y": 14,
      "Side": "East",
      "Id": "door-edge-4"
    },
    {
      "X": 15,
      "Y": 15,
      "Side": "South",
      "Id": "door-edge-5"
    },
    {
      "X": 16,
      "Y": 15,
      "Side": "South",
      "Id": "door-edge-5"
    },
    {
      "X": 17,
      "Y": 15,
      "Side": "South",
      "Id": "door-edge-5"
    },
    {
      "X": 15,
      "Y": 7,
      "Side": "South",
      "Id": "door-edge-6"
    },
    {
      "X": 16,
      "Y": 7,
      "Side": "South",
      "Id": "door-edge-6"
    },
    {
      "X": 17,
      "Y": 7,
      "Side": "South",
      "Id": "door-edge-6"
    },
    {
      "X": 12,
      "Y": 6,
      "Side": "East",
      "Id": "door-edge-7"
    },
    {
      "X": 19,
      "Y": 6,
      "Side": "East",
      "Id": "door-edge-8"
    },
    {
      "X": 7,
      "Y": 21,
      "Side": "South",
      "Id": "door-edge-9"
    },
    {
      "X": 8,
      "Y": 21,
      "Side": "South",
      "Id": "door-edge-9"
    },
    {
      "X": 24,
      "Y": 21,
      "Side": "South",
      "Id": "door-edge-10"
    },
    {
      "X": 25,
      "Y": 21,
      "Side": "South",
      "Id": "door-edge-10"
    },
    {
      "X": 31,
      "Y": 12,
      "Side": "South",
      "Id": "door-edge-11"
    },
    {
      "X": 1,
      "Y": 12,
      "Side": "South",
      "Id": "door-edge-13"
    }
  ],
  "WreckPatches": []
}
""";
}
