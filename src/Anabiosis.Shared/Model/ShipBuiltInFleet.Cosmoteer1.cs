namespace Anabiosis.Shared.Model;

// Raw JSON captured verbatim from %LocalAppData%\Anabiosis\custom-ships\cosmoteer1.json - see
// ShipBuiltInFleet.cs's own doc comment for why this is frozen here instead of read from disk.
public static partial class ShipBuiltInFleet
{
    private const string Cosmoteer1Json = """
{
  "Name": "\u0420\u0435\u0430\u043A\u0442\u043E\u0440\u043D\u044B\u0439 \u043E\u0442\u0441\u0435\u043A",
  "Rooms": [
    {
      "Id": "room-20",
      "Name": "\u041E\u0442\u0441\u0435\u043A 20",
      "Rects": [
        {
          "X": 6,
          "Y": -3,
          "Width": 3,
          "Height": 3,
          "Left": 6,
          "Right": 9,
          "Top": -3,
          "Bottom": 0,
          "Area": 9,
          "Center": {
            "X": 7.5,
            "Y": -1.5
          }
        }
      ],
      "X": 6,
      "Y": -3,
      "Width": 3,
      "Height": 3
    },
    {
      "Id": "room-32",
      "Name": "\u041E\u0442\u0441\u0435\u043A 32",
      "Rects": [
        {
          "X": 15,
          "Y": -6,
          "Width": 3,
          "Height": 3,
          "Left": 15,
          "Right": 18,
          "Top": -6,
          "Bottom": -3,
          "Area": 9,
          "Center": {
            "X": 16.5,
            "Y": -4.5
          }
        }
      ],
      "X": 15,
      "Y": -6,
      "Width": 3,
      "Height": 3
    },
    {
      "Id": "room-77",
      "Name": "\u041E\u0442\u0441\u0435\u043A 77",
      "Rects": [
        {
          "X": 3,
          "Y": 6,
          "Width": 3,
          "Height": 3,
          "Left": 3,
          "Right": 6,
          "Top": 6,
          "Bottom": 9,
          "Area": 9,
          "Center": {
            "X": 4.5,
            "Y": 7.5
          }
        }
      ],
      "X": 3,
      "Y": 6,
      "Width": 3,
      "Height": 3
    },
    {
      "Id": "room-34",
      "Name": "\u041E\u0442\u0441\u0435\u043A 34",
      "Rects": [
        {
          "X": 6,
          "Y": -6,
          "Width": 3,
          "Height": 3,
          "Left": 6,
          "Right": 9,
          "Top": -6,
          "Bottom": -3,
          "Area": 9,
          "Center": {
            "X": 7.5,
            "Y": -4.5
          }
        }
      ],
      "X": 6,
      "Y": -6,
      "Width": 3,
      "Height": 3
    },
    {
      "Id": "room-36",
      "Name": "\u041E\u0442\u0441\u0435\u043A 36",
      "Rects": [
        {
          "X": 16,
          "Y": -12,
          "Width": 2,
          "Height": 6,
          "Left": 16,
          "Right": 18,
          "Top": -12,
          "Bottom": -6,
          "Area": 12,
          "Center": {
            "X": 17,
            "Y": -9
          }
        },
        {
          "X": 15,
          "Y": -8,
          "Width": 1,
          "Height": 1,
          "Left": 15,
          "Right": 16,
          "Top": -8,
          "Bottom": -7,
          "Area": 1,
          "Center": {
            "X": 15.5,
            "Y": -7.5
          }
        }
      ],
      "X": 15,
      "Y": -12,
      "Width": 3,
      "Height": 6
    },
    {
      "Id": "room-38",
      "Name": "\u041E\u0442\u0441\u0435\u043A 38",
      "Rects": [
        {
          "X": 6,
          "Y": -12,
          "Width": 2,
          "Height": 6,
          "Left": 6,
          "Right": 8,
          "Top": -12,
          "Bottom": -6,
          "Area": 12,
          "Center": {
            "X": 7,
            "Y": -9
          }
        },
        {
          "X": 8,
          "Y": -8,
          "Width": 1,
          "Height": 1,
          "Left": 8,
          "Right": 9,
          "Top": -8,
          "Bottom": -7,
          "Area": 1,
          "Center": {
            "X": 8.5,
            "Y": -7.5
          }
        }
      ],
      "X": 6,
      "Y": -12,
      "Width": 3,
      "Height": 6
    },
    {
      "Id": "room-40",
      "Name": "\u041E\u0442\u0441\u0435\u043A 40",
      "Rects": [
        {
          "X": 12,
          "Y": -15,
          "Width": 3,
          "Height": 6,
          "Left": 12,
          "Right": 15,
          "Top": -15,
          "Bottom": -9,
          "Area": 18,
          "Center": {
            "X": 13.5,
            "Y": -12
          }
        }
      ],
      "X": 12,
      "Y": -15,
      "Width": 3,
      "Height": 6
    },
    {
      "Id": "room-42",
      "Name": "\u041E\u0442\u0441\u0435\u043A 42",
      "Rects": [
        {
          "X": 9,
          "Y": -15,
          "Width": 3,
          "Height": 6,
          "Left": 9,
          "Right": 12,
          "Top": -15,
          "Bottom": -9,
          "Area": 18,
          "Center": {
            "X": 10.5,
            "Y": -12
          }
        }
      ],
      "X": 9,
      "Y": -15,
      "Width": 3,
      "Height": 6
    },
    {
      "Id": "room-44",
      "Name": "\u041E\u0442\u0441\u0435\u043A 44",
      "Rects": [
        {
          "X": 13,
          "Y": -3,
          "Width": 5,
          "Height": 6,
          "Left": 13,
          "Right": 18,
          "Top": -3,
          "Bottom": 3,
          "Area": 30,
          "Center": {
            "X": 15.5,
            "Y": 0
          }
        },
        {
          "X": 12,
          "Y": -1,
          "Width": 1,
          "Height": 2,
          "Left": 12,
          "Right": 13,
          "Top": -1,
          "Bottom": 1,
          "Area": 2,
          "Center": {
            "X": 12.5,
            "Y": 0
          }
        }
      ],
      "X": 12,
      "Y": -3,
      "Width": 6,
      "Height": 6
    },
    {
      "Id": "room-79",
      "Name": "\u041E\u0442\u0441\u0435\u043A 79",
      "Rects": [
        {
          "X": 18,
          "Y": 6,
          "Width": 3,
          "Height": 3,
          "Left": 18,
          "Right": 21,
          "Top": 6,
          "Bottom": 9,
          "Area": 9,
          "Center": {
            "X": 19.5,
            "Y": 7.5
          }
        }
      ],
      "X": 18,
      "Y": 6,
      "Width": 3,
      "Height": 3
    },
    {
      "Id": "room-48",
      "Name": "\u041E\u0442\u0441\u0435\u043A 48",
      "Rects": [
        {
          "X": 13,
          "Y": 3,
          "Width": 4,
          "Height": 6,
          "Left": 13,
          "Right": 17,
          "Top": 3,
          "Bottom": 9,
          "Area": 24,
          "Center": {
            "X": 15,
            "Y": 6
          }
        },
        {
          "X": 12,
          "Y": 5,
          "Width": 1,
          "Height": 2,
          "Left": 12,
          "Right": 13,
          "Top": 5,
          "Bottom": 7,
          "Area": 2,
          "Center": {
            "X": 12.5,
            "Y": 6
          }
        },
        {
          "X": 17,
          "Y": 7,
          "Width": 1,
          "Height": 1,
          "Left": 17,
          "Right": 18,
          "Top": 7,
          "Bottom": 8,
          "Area": 1,
          "Center": {
            "X": 17.5,
            "Y": 7.5
          }
        }
      ],
      "X": 12,
      "Y": 3,
      "Width": 6,
      "Height": 6
    },
    {
      "Id": "room-81",
      "Name": "\u041E\u0442\u0441\u0435\u043A 81",
      "Rects": [
        {
          "X": 6,
          "Y": 9,
          "Width": 2,
          "Height": 5,
          "Left": 6,
          "Right": 8,
          "Top": 9,
          "Bottom": 14,
          "Area": 10,
          "Center": {
            "X": 7,
            "Y": 11.5
          }
        },
        {
          "X": 4,
          "Y": 10,
          "Width": 2,
          "Height": 5,
          "Left": 4,
          "Right": 6,
          "Top": 10,
          "Bottom": 15,
          "Area": 10,
          "Center": {
            "X": 5,
            "Y": 12.5
          }
        },
        {
          "X": 8,
          "Y": 12,
          "Width": 1,
          "Height": 2,
          "Left": 8,
          "Right": 9,
          "Top": 12,
          "Bottom": 14,
          "Area": 2,
          "Center": {
            "X": 8.5,
            "Y": 13
          }
        },
        {
          "X": 3,
          "Y": 13,
          "Width": 1,
          "Height": 1,
          "Left": 3,
          "Right": 4,
          "Top": 13,
          "Bottom": 14,
          "Area": 1,
          "Center": {
            "X": 3.5,
            "Y": 13.5
          }
        },
        {
          "X": 7,
          "Y": 14,
          "Width": 1,
          "Height": 1,
          "Left": 7,
          "Right": 8,
          "Top": 14,
          "Bottom": 15,
          "Area": 1,
          "Center": {
            "X": 7.5,
            "Y": 14.5
          }
        }
      ],
      "X": 3,
      "Y": 9,
      "Width": 6,
      "Height": 6
    },
    {
      "Id": "room-73",
      "Name": "\u041E\u0442\u0441\u0435\u043A 73",
      "Rects": [
        {
          "X": 16,
          "Y": 9,
          "Width": 4,
          "Height": 5,
          "Left": 16,
          "Right": 20,
          "Top": 9,
          "Bottom": 14,
          "Area": 20,
          "Center": {
            "X": 18,
            "Y": 11.5
          }
        },
        {
          "X": 15,
          "Y": 12,
          "Width": 1,
          "Height": 2,
          "Left": 15,
          "Right": 16,
          "Top": 12,
          "Bottom": 14,
          "Area": 2,
          "Center": {
            "X": 15.5,
            "Y": 13
          }
        },
        {
          "X": 20,
          "Y": 13,
          "Width": 1,
          "Height": 2,
          "Left": 20,
          "Right": 21,
          "Top": 13,
          "Bottom": 15,
          "Area": 2,
          "Center": {
            "X": 20.5,
            "Y": 14
          }
        },
        {
          "X": 16,
          "Y": 14,
          "Width": 1,
          "Height": 1,
          "Left": 16,
          "Right": 17,
          "Top": 14,
          "Bottom": 15,
          "Area": 1,
          "Center": {
            "X": 16.5,
            "Y": 14.5
          }
        }
      ],
      "X": 15,
      "Y": 9,
      "Width": 6,
      "Height": 6
    },
    {
      "Id": "room-71",
      "Name": "\u041E\u0442\u0441\u0435\u043A 71",
      "Rects": [
        {
          "X": 10,
          "Y": 9,
          "Width": 2,
          "Height": 5,
          "Left": 10,
          "Right": 12,
          "Top": 9,
          "Bottom": 14,
          "Area": 10,
          "Center": {
            "X": 11,
            "Y": 11.5
          }
        },
        {
          "X": 12,
          "Y": 10,
          "Width": 2,
          "Height": 4,
          "Left": 12,
          "Right": 14,
          "Top": 10,
          "Bottom": 14,
          "Area": 8,
          "Center": {
            "X": 13,
            "Y": 12
          }
        },
        {
          "X": 9,
          "Y": 12,
          "Width": 1,
          "Height": 2,
          "Left": 9,
          "Right": 10,
          "Top": 12,
          "Bottom": 14,
          "Area": 2,
          "Center": {
            "X": 9.5,
            "Y": 13
          }
        },
        {
          "X": 14,
          "Y": 12,
          "Width": 1,
          "Height": 2,
          "Left": 14,
          "Right": 15,
          "Top": 12,
          "Bottom": 14,
          "Area": 2,
          "Center": {
            "X": 14.5,
            "Y": 13
          }
        },
        {
          "X": 10,
          "Y": 14,
          "Width": 1,
          "Height": 1,
          "Left": 10,
          "Right": 11,
          "Top": 14,
          "Bottom": 15,
          "Area": 1,
          "Center": {
            "X": 10.5,
            "Y": 14.5
          }
        },
        {
          "X": 13,
          "Y": 14,
          "Width": 1,
          "Height": 1,
          "Left": 13,
          "Right": 14,
          "Top": 14,
          "Bottom": 15,
          "Area": 1,
          "Center": {
            "X": 13.5,
            "Y": 14.5
          }
        }
      ],
      "X": 9,
      "Y": 9,
      "Width": 6,
      "Height": 6
    },
    {
      "Id": "room-56",
      "Name": "\u041E\u0442\u0441\u0435\u043A 56",
      "Rects": [
        {
          "X": 6,
          "Y": 0,
          "Width": 2,
          "Height": 6,
          "Left": 6,
          "Right": 8,
          "Top": 0,
          "Bottom": 6,
          "Area": 12,
          "Center": {
            "X": 7,
            "Y": 3
          }
        },
        {
          "X": 8,
          "Y": 3,
          "Width": 1,
          "Height": 3,
          "Left": 8,
          "Right": 9,
          "Top": 3,
          "Bottom": 6,
          "Area": 3,
          "Center": {
            "X": 8.5,
            "Y": 4.5
          }
        }
      ],
      "X": 6,
      "Y": 0,
      "Width": 3,
      "Height": 6
    },
    {
      "Id": "room-59",
      "Name": "\u041E\u0442\u0441\u0435\u043A 59",
      "Rects": [
        {
          "X": 24,
          "Y": 12,
          "Width": 3,
          "Height": 2,
          "Left": 24,
          "Right": 27,
          "Top": 12,
          "Bottom": 14,
          "Area": 6,
          "Center": {
            "X": 25.5,
            "Y": 13
          }
        },
        {
          "X": 25,
          "Y": 14,
          "Width": 1,
          "Height": 1,
          "Left": 25,
          "Right": 26,
          "Top": 14,
          "Bottom": 15,
          "Area": 1,
          "Center": {
            "X": 25.5,
            "Y": 14.5
          }
        }
      ],
      "X": 24,
      "Y": 12,
      "Width": 3,
      "Height": 3
    },
    {
      "Id": "room-60",
      "Name": "\u041E\u0442\u0441\u0435\u043A 60",
      "Rects": [
        {
          "X": 24,
          "Y": 15,
          "Width": 3,
          "Height": 3,
          "Left": 24,
          "Right": 27,
          "Top": 15,
          "Bottom": 18,
          "Area": 9,
          "Center": {
            "X": 25.5,
            "Y": 16.5
          }
        }
      ],
      "X": 24,
      "Y": 15,
      "Width": 3,
      "Height": 3
    },
    {
      "Id": "room-62",
      "Name": "\u041E\u0442\u0441\u0435\u043A 62",
      "Rects": [
        {
          "X": 21,
          "Y": 12,
          "Width": 3,
          "Height": 3,
          "Left": 21,
          "Right": 24,
          "Top": 12,
          "Bottom": 15,
          "Area": 9,
          "Center": {
            "X": 22.5,
            "Y": 13.5
          }
        }
      ],
      "X": 21,
      "Y": 12,
      "Width": 3,
      "Height": 3
    },
    {
      "Id": "room-64",
      "Name": "\u041E\u0442\u0441\u0435\u043A 64",
      "Rects": [
        {
          "X": -3,
          "Y": 15,
          "Width": 3,
          "Height": 3,
          "Left": -3,
          "Right": 0,
          "Top": 15,
          "Bottom": 18,
          "Area": 9,
          "Center": {
            "X": -1.5,
            "Y": 16.5
          }
        }
      ],
      "X": -3,
      "Y": 15,
      "Width": 3,
      "Height": 3
    },
    {
      "Id": "room-66",
      "Name": "\u041E\u0442\u0441\u0435\u043A 66",
      "Rects": [
        {
          "X": -3,
          "Y": 12,
          "Width": 3,
          "Height": 2,
          "Left": -3,
          "Right": 0,
          "Top": 12,
          "Bottom": 14,
          "Area": 6,
          "Center": {
            "X": -1.5,
            "Y": 13
          }
        },
        {
          "X": -2,
          "Y": 14,
          "Width": 1,
          "Height": 1,
          "Left": -2,
          "Right": -1,
          "Top": 14,
          "Bottom": 15,
          "Area": 1,
          "Center": {
            "X": -1.5,
            "Y": 14.5
          }
        }
      ],
      "X": -3,
      "Y": 12,
      "Width": 3,
      "Height": 3
    },
    {
      "Id": "room-68",
      "Name": "\u041E\u0442\u0441\u0435\u043A 68",
      "Rects": [
        {
          "X": 6,
          "Y": 15,
          "Width": 3,
          "Height": 3,
          "Left": 6,
          "Right": 9,
          "Top": 15,
          "Bottom": 18,
          "Area": 9,
          "Center": {
            "X": 7.5,
            "Y": 16.5
          }
        }
      ],
      "X": 6,
      "Y": 15,
      "Width": 3,
      "Height": 3
    },
    {
      "Id": "room-70",
      "Name": "\u041E\u0442\u0441\u0435\u043A 70",
      "Rects": [
        {
          "X": 9,
          "Y": 15,
          "Width": 3,
          "Height": 6,
          "Left": 9,
          "Right": 12,
          "Top": 15,
          "Bottom": 21,
          "Area": 18,
          "Center": {
            "X": 10.5,
            "Y": 18
          }
        }
      ],
      "X": 9,
      "Y": 15,
      "Width": 3,
      "Height": 6
    },
    {
      "Id": "room-72",
      "Name": "\u041E\u0442\u0441\u0435\u043A 72",
      "Rects": [
        {
          "X": 12,
          "Y": 15,
          "Width": 3,
          "Height": 6,
          "Left": 12,
          "Right": 15,
          "Top": 15,
          "Bottom": 21,
          "Area": 18,
          "Center": {
            "X": 13.5,
            "Y": 18
          }
        }
      ],
      "X": 12,
      "Y": 15,
      "Width": 3,
      "Height": 6
    },
    {
      "Id": "room-74",
      "Name": "\u041E\u0442\u0441\u0435\u043A 74",
      "Rects": [
        {
          "X": 15,
          "Y": 15,
          "Width": 3,
          "Height": 3,
          "Left": 15,
          "Right": 18,
          "Top": 15,
          "Bottom": 18,
          "Area": 9,
          "Center": {
            "X": 16.5,
            "Y": 16.5
          }
        }
      ],
      "X": 15,
      "Y": 15,
      "Width": 3,
      "Height": 3
    },
    {
      "Id": "room-76",
      "Name": "\u041E\u0442\u0441\u0435\u043A 76",
      "Rects": [
        {
          "X": 9,
          "Y": -9,
          "Width": 6,
          "Height": 6,
          "Left": 9,
          "Right": 15,
          "Top": -9,
          "Bottom": -3,
          "Area": 36,
          "Center": {
            "X": 12,
            "Y": -6
          }
        },
        {
          "X": 9,
          "Y": -3,
          "Width": 3,
          "Height": 12,
          "Left": 9,
          "Right": 12,
          "Top": -3,
          "Bottom": 9,
          "Area": 36,
          "Center": {
            "X": 10.5,
            "Y": 3
          }
        },
        {
          "X": 6,
          "Y": 6,
          "Width": 3,
          "Height": 3,
          "Left": 6,
          "Right": 9,
          "Top": 6,
          "Bottom": 9,
          "Area": 9,
          "Center": {
            "X": 7.5,
            "Y": 7.5
          }
        }
      ],
      "X": 6,
      "Y": -9,
      "Width": 9,
      "Height": 18
    },
    {
      "Id": "room-82",
      "Name": "\u041E\u0442\u0441\u0435\u043A 82",
      "Rects": [
        {
          "X": 0,
          "Y": 12,
          "Width": 3,
          "Height": 3,
          "Left": 0,
          "Right": 3,
          "Top": 12,
          "Bottom": 15,
          "Area": 9,
          "Center": {
            "X": 1.5,
            "Y": 13.5
          }
        }
      ],
      "X": 0,
      "Y": 12,
      "Width": 3,
      "Height": 3
    }
  ],
  "Doors": [],
  "Airlocks": [],
  "Devices": [
    {
      "Kind": "Distribution",
      "X": 10.5,
      "Y": -7.5,
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
      "Kind": "Oxygen",
      "X": 11.5,
      "Y": -7.5,
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
      "X": 12.5,
      "Y": -7.5,
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
      "X": 13.5,
      "Y": -7.5,
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
      "Kind": "Sofa",
      "X": 13.5,
      "Y": -1.5,
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
      "Kind": "Sofa",
      "X": 13.5,
      "Y": 1.5,
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
      "Kind": "Nightstand",
      "X": 14.5,
      "Y": -1.5,
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
      "Kind": "Nightstand",
      "X": 14.5,
      "Y": 1.5,
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
      "Kind": "Bed",
      "X": 16.5,
      "Y": -1,
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
      "Kind": "Bed",
      "X": 16.5,
      "Y": 1,
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
      "Kind": "Navigation",
      "X": 14,
      "Y": 4,
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
      "Kind": "CommsConsole",
      "X": 14,
      "Y": 8,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "HalfWidthSide": "South",
      "Id": null
    },
    {
      "Kind": "Helm",
      "X": 16,
      "Y": 4,
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
      "Kind": "ShipStatusMonitor",
      "X": 16,
      "Y": 8,
      "MountSide": "Aft",
      "CameraSide": null,
      "TargetDoorId": null,
      "ThrustBonus": 0,
      "TurnBonus": 0,
      "CapacityBonus": 0,
      "WallDeviceFacingSide": null,
      "Rotated": true,
      "HalfWidthSide": "South",
      "Id": null
    },
    {
      "Kind": "Reactor",
      "X": 12,
      "Y": 12,
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
      "Kind": "Fabricator",
      "X": 20,
      "Y": 11.5,
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
      "X": 4,
      "Y": 11.5,
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
      "Kind": "Bed",
      "X": 7.5,
      "Y": 2,
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
    }
  ],
  "ForwardDegrees": 0,
  "WallMaterialsRaw": [],
  "EnginesRaw": [
    {
      "X": 7.5,
      "Y": -4.5,
      "Facing": "West",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 16.5,
      "Y": -4.5,
      "Facing": "East",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 25.5,
      "Y": 13.5,
      "Facing": "North",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 25.5,
      "Y": 16.5,
      "Facing": "East",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": -1.5,
      "Y": 16.5,
      "Facing": "West",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": -1.5,
      "Y": 13.5,
      "Facing": "North",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 7.5,
      "Y": 16.5,
      "Facing": "South",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 10.5,
      "Y": 19.5,
      "Facing": "South",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 13.5,
      "Y": 19.5,
      "Facing": "South",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 16.5,
      "Y": 16.5,
      "Facing": "South",
      "MaxThrust": 8,
      "Role": "Marching"
    }
  ],
  "SupplementalWallTilesRaw": [
    {
      "X": 6,
      "Y": -4
    },
    {
      "X": 6,
      "Y": 0
    },
    {
      "X": 17,
      "Y": -7
    },
    {
      "X": 17,
      "Y": -3
    },
    {
      "X": 3,
      "Y": 9
    },
    {
      "X": 6,
      "Y": -7
    },
    {
      "X": 6,
      "Y": -3
    },
    {
      "X": 15,
      "Y": -10
    },
    {
      "X": 15,
      "Y": -11
    },
    {
      "X": 15,
      "Y": -12
    },
    {
      "X": 17,
      "Y": -6
    },
    {
      "X": 8,
      "Y": -10
    },
    {
      "X": 8,
      "Y": -11
    },
    {
      "X": 8,
      "Y": -12
    },
    {
      "X": 6,
      "Y": -6
    },
    {
      "X": 11,
      "Y": -15
    },
    {
      "X": 12,
      "Y": -15
    },
    {
      "X": 17,
      "Y": 3
    },
    {
      "X": 17,
      "Y": -4
    },
    {
      "X": 20,
      "Y": 9
    },
    {
      "X": 17,
      "Y": 6
    },
    {
      "X": 17,
      "Y": 5
    },
    {
      "X": 17,
      "Y": 4
    },
    {
      "X": 17,
      "Y": 8
    },
    {
      "X": 14,
      "Y": 9
    },
    {
      "X": 15,
      "Y": 9
    },
    {
      "X": 17,
      "Y": 2
    },
    {
      "X": 5,
      "Y": 9
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
      "X": 4,
      "Y": 9
    },
    {
      "X": 3,
      "Y": 10
    },
    {
      "X": 6,
      "Y": 14
    },
    {
      "X": 3,
      "Y": 11
    },
    {
      "X": 3,
      "Y": 12
    },
    {
      "X": 8,
      "Y": 14
    },
    {
      "X": 3,
      "Y": 14
    },
    {
      "X": 15,
      "Y": 11
    },
    {
      "X": 15,
      "Y": 14
    },
    {
      "X": 17,
      "Y": 14
    },
    {
      "X": 15,
      "Y": 10
    },
    {
      "X": 18,
      "Y": 14
    },
    {
      "X": 20,
      "Y": 12
    },
    {
      "X": 19,
      "Y": 14
    },
    {
      "X": 20,
      "Y": 11
    },
    {
      "X": 20,
      "Y": 10
    },
    {
      "X": 9,
      "Y": 11
    },
    {
      "X": 9,
      "Y": 14
    },
    {
      "X": 11,
      "Y": 14
    },
    {
      "X": 9,
      "Y": 10
    },
    {
      "X": 12,
      "Y": 14
    },
    {
      "X": 12,
      "Y": 9
    },
    {
      "X": 14,
      "Y": 11
    },
    {
      "X": 14,
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
      "X": 6,
      "Y": -1
    },
    {
      "X": 24,
      "Y": 14
    },
    {
      "X": 26,
      "Y": 14
    },
    {
      "X": -1,
      "Y": 14
    },
    {
      "X": -3,
      "Y": 14
    },
    {
      "X": 8,
      "Y": 17
    },
    {
      "X": 12,
      "Y": 20
    },
    {
      "X": 15,
      "Y": 17
    },
    {
      "X": 11,
      "Y": 20
    },
    {
      "X": 6,
      "Y": 5
    },
    {
      "X": 12,
      "Y": 8
    },
    {
      "X": 12,
      "Y": 3
    },
    {
      "X": 8,
      "Y": 0
    },
    {
      "X": 12,
      "Y": 2
    },
    {
      "X": 8,
      "Y": -1
    },
    {
      "X": 8,
      "Y": -3
    },
    {
      "X": 12,
      "Y": -3
    },
    {
      "X": 16,
      "Y": -4
    },
    {
      "X": 7,
      "Y": -4
    },
    {
      "X": 8,
      "Y": -4
    },
    {
      "X": 8,
      "Y": -9
    },
    {
      "X": 8,
      "Y": -7
    },
    {
      "X": 15,
      "Y": -9
    },
    {
      "X": 15,
      "Y": -7
    },
    {
      "X": 11,
      "Y": -14
    },
    {
      "X": 11,
      "Y": -13
    },
    {
      "X": 11,
      "Y": -12
    },
    {
      "X": 11,
      "Y": -11
    },
    {
      "X": 11,
      "Y": -10
    },
    {
      "X": 12,
      "Y": -2
    },
    {
      "X": 12,
      "Y": 1
    },
    {
      "X": 14,
      "Y": 2
    },
    {
      "X": 15,
      "Y": 2
    },
    {
      "X": 16,
      "Y": 2
    },
    {
      "X": 12,
      "Y": 4
    },
    {
      "X": 12,
      "Y": 7
    },
    {
      "X": 19,
      "Y": 8
    },
    {
      "X": 9,
      "Y": 9
    },
    {
      "X": 8,
      "Y": 9
    },
    {
      "X": 7,
      "Y": -1
    },
    {
      "X": 8,
      "Y": -2
    },
    {
      "X": 7,
      "Y": 5
    },
    {
      "X": 8,
      "Y": 1
    },
    {
      "X": 8,
      "Y": 2
    },
    {
      "X": 8,
      "Y": 5
    },
    {
      "X": 11,
      "Y": 16
    },
    {
      "X": 11,
      "Y": 17
    },
    {
      "X": 11,
      "Y": 18
    },
    {
      "X": 11,
      "Y": 19
    },
    {
      "X": 14,
      "Y": 16
    },
    {
      "X": 14,
      "Y": 17
    },
    {
      "X": 8,
      "Y": 16
    }
  ],
  "ForcedFloorTilesRaw": [
    {
      "X": 7,
      "Y": -2
    },
    {
      "X": 15,
      "Y": -5
    },
    {
      "X": 16,
      "Y": -5
    },
    {
      "X": 3,
      "Y": 7
    },
    {
      "X": 4,
      "Y": 7
    },
    {
      "X": 5,
      "Y": 7
    },
    {
      "X": 8,
      "Y": -5
    },
    {
      "X": 7,
      "Y": -5
    },
    {
      "X": 15,
      "Y": -8
    },
    {
      "X": 16,
      "Y": -8
    },
    {
      "X": 16,
      "Y": -9
    },
    {
      "X": 16,
      "Y": -10
    },
    {
      "X": 16,
      "Y": -11
    },
    {
      "X": 8,
      "Y": -8
    },
    {
      "X": 7,
      "Y": -8
    },
    {
      "X": 7,
      "Y": -9
    },
    {
      "X": 7,
      "Y": -10
    },
    {
      "X": 7,
      "Y": -11
    },
    {
      "X": 13,
      "Y": -10
    },
    {
      "X": 13,
      "Y": -11
    },
    {
      "X": 13,
      "Y": -12
    },
    {
      "X": 13,
      "Y": -13
    },
    {
      "X": 13,
      "Y": -14
    },
    {
      "X": 10,
      "Y": -10
    },
    {
      "X": 10,
      "Y": -11
    },
    {
      "X": 10,
      "Y": -12
    },
    {
      "X": 10,
      "Y": -13
    },
    {
      "X": 10,
      "Y": -14
    },
    {
      "X": 12,
      "Y": 0
    },
    {
      "X": 12,
      "Y": -1
    },
    {
      "X": 13,
      "Y": 0
    },
    {
      "X": 13,
      "Y": -1
    },
    {
      "X": 13,
      "Y": 1
    },
    {
      "X": 14,
      "Y": 0
    },
    {
      "X": 13,
      "Y": -2
    },
    {
      "X": 14,
      "Y": -1
    },
    {
      "X": 14,
      "Y": 1
    },
    {
      "X": 15,
      "Y": 0
    },
    {
      "X": 14,
      "Y": -2
    },
    {
      "X": 15,
      "Y": -1
    },
    {
      "X": 15,
      "Y": 1
    },
    {
      "X": 16,
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
      "X": 16,
      "Y": 1
    },
    {
      "X": 16,
      "Y": -2
    },
    {
      "X": 18,
      "Y": 7
    },
    {
      "X": 19,
      "Y": 7
    },
    {
      "X": 20,
      "Y": 7
    },
    {
      "X": 12,
      "Y": 6
    },
    {
      "X": 12,
      "Y": 5
    },
    {
      "X": 13,
      "Y": 6
    },
    {
      "X": 13,
      "Y": 5
    },
    {
      "X": 13,
      "Y": 7
    },
    {
      "X": 14,
      "Y": 6
    },
    {
      "X": 13,
      "Y": 4
    },
    {
      "X": 14,
      "Y": 5
    },
    {
      "X": 14,
      "Y": 7
    },
    {
      "X": 15,
      "Y": 6
    },
    {
      "X": 14,
      "Y": 4
    },
    {
      "X": 15,
      "Y": 5
    },
    {
      "X": 15,
      "Y": 7
    },
    {
      "X": 16,
      "Y": 6
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
      "X": 16,
      "Y": 7
    },
    {
      "X": 16,
      "Y": 4
    },
    {
      "X": 17,
      "Y": 7
    },
    {
      "X": 6,
      "Y": 9
    },
    {
      "X": 6,
      "Y": 10
    },
    {
      "X": 7,
      "Y": 9
    },
    {
      "X": 6,
      "Y": 11
    },
    {
      "X": 7,
      "Y": 10
    },
    {
      "X": 5,
      "Y": 10
    },
    {
      "X": 6,
      "Y": 12
    },
    {
      "X": 7,
      "Y": 11
    },
    {
      "X": 5,
      "Y": 11
    },
    {
      "X": 4,
      "Y": 10
    },
    {
      "X": 6,
      "Y": 13
    },
    {
      "X": 7,
      "Y": 12
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
      "X": 7,
      "Y": 13
    },
    {
      "X": 5,
      "Y": 13
    },
    {
      "X": 8,
      "Y": 12
    },
    {
      "X": 4,
      "Y": 12
    },
    {
      "X": 7,
      "Y": 14
    },
    {
      "X": 8,
      "Y": 13
    },
    {
      "X": 4,
      "Y": 13
    },
    {
      "X": 3,
      "Y": 13
    },
    {
      "X": 15,
      "Y": 12
    },
    {
      "X": 15,
      "Y": 13
    },
    {
      "X": 16,
      "Y": 12
    },
    {
      "X": 16,
      "Y": 13
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
      "X": 16,
      "Y": 14
    },
    {
      "X": 17,
      "Y": 13
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
      "X": 18,
      "Y": 13
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
      "X": 19,
      "Y": 13
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
      "X": 20,
      "Y": 13
    },
    {
      "X": 19,
      "Y": 10
    },
    {
      "X": 9,
      "Y": 12
    },
    {
      "X": 9,
      "Y": 13
    },
    {
      "X": 10,
      "Y": 12
    },
    {
      "X": 10,
      "Y": 13
    },
    {
      "X": 10,
      "Y": 11
    },
    {
      "X": 11,
      "Y": 12
    },
    {
      "X": 10,
      "Y": 14
    },
    {
      "X": 11,
      "Y": 13
    },
    {
      "X": 10,
      "Y": 10
    },
    {
      "X": 11,
      "Y": 11
    },
    {
      "X": 12,
      "Y": 12
    },
    {
      "X": 12,
      "Y": 13
    },
    {
      "X": 10,
      "Y": 9
    },
    {
      "X": 11,
      "Y": 10
    },
    {
      "X": 12,
      "Y": 11
    },
    {
      "X": 13,
      "Y": 12
    },
    {
      "X": 13,
      "Y": 13
    },
    {
      "X": 11,
      "Y": 9
    },
    {
      "X": 12,
      "Y": 10
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
      "X": 13,
      "Y": 14
    },
    {
      "X": 14,
      "Y": 13
    },
    {
      "X": 13,
      "Y": 10
    },
    {
      "X": 8,
      "Y": 4
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
      "X": 7,
      "Y": 3
    },
    {
      "X": 7,
      "Y": 2
    },
    {
      "X": 7,
      "Y": 1
    },
    {
      "X": 24,
      "Y": 13
    },
    {
      "X": 25,
      "Y": 13
    },
    {
      "X": 25,
      "Y": 14
    },
    {
      "X": 25,
      "Y": 15
    },
    {
      "X": 25,
      "Y": 16
    },
    {
      "X": 21,
      "Y": 13
    },
    {
      "X": 22,
      "Y": 13
    },
    {
      "X": 23,
      "Y": 13
    },
    {
      "X": -2,
      "Y": 15
    },
    {
      "X": -2,
      "Y": 16
    },
    {
      "X": -1,
      "Y": 13
    },
    {
      "X": -2,
      "Y": 13
    },
    {
      "X": -2,
      "Y": 14
    },
    {
      "X": 7,
      "Y": 15
    },
    {
      "X": 7,
      "Y": 16
    },
    {
      "X": 10,
      "Y": 15
    },
    {
      "X": 10,
      "Y": 16
    },
    {
      "X": 10,
      "Y": 17
    },
    {
      "X": 10,
      "Y": 18
    },
    {
      "X": 10,
      "Y": 19
    },
    {
      "X": 13,
      "Y": 15
    },
    {
      "X": 13,
      "Y": 16
    },
    {
      "X": 13,
      "Y": 17
    },
    {
      "X": 13,
      "Y": 18
    },
    {
      "X": 13,
      "Y": 19
    },
    {
      "X": 16,
      "Y": 15
    },
    {
      "X": 16,
      "Y": 16
    },
    {
      "X": 6,
      "Y": 7
    },
    {
      "X": 6,
      "Y": 6
    },
    {
      "X": 6,
      "Y": 8
    },
    {
      "X": 7,
      "Y": 7
    },
    {
      "X": 7,
      "Y": 6
    },
    {
      "X": 7,
      "Y": 8
    },
    {
      "X": 8,
      "Y": 7
    },
    {
      "X": 8,
      "Y": 6
    },
    {
      "X": 8,
      "Y": 8
    },
    {
      "X": 9,
      "Y": 7
    },
    {
      "X": 9,
      "Y": 6
    },
    {
      "X": 9,
      "Y": 8
    },
    {
      "X": 10,
      "Y": 7
    },
    {
      "X": 9,
      "Y": 5
    },
    {
      "X": 10,
      "Y": 6
    },
    {
      "X": 10,
      "Y": 8
    },
    {
      "X": 11,
      "Y": 7
    },
    {
      "X": 9,
      "Y": 4
    },
    {
      "X": 10,
      "Y": 5
    },
    {
      "X": 11,
      "Y": 6
    },
    {
      "X": 11,
      "Y": 8
    },
    {
      "X": 9,
      "Y": 3
    },
    {
      "X": 10,
      "Y": 4
    },
    {
      "X": 11,
      "Y": 5
    },
    {
      "X": 9,
      "Y": 2
    },
    {
      "X": 10,
      "Y": 3
    },
    {
      "X": 11,
      "Y": 4
    },
    {
      "X": 9,
      "Y": 1
    },
    {
      "X": 10,
      "Y": 2
    },
    {
      "X": 11,
      "Y": 3
    },
    {
      "X": 9,
      "Y": 0
    },
    {
      "X": 10,
      "Y": 1
    },
    {
      "X": 11,
      "Y": 2
    },
    {
      "X": 9,
      "Y": -1
    },
    {
      "X": 10,
      "Y": 0
    },
    {
      "X": 11,
      "Y": 1
    },
    {
      "X": 9,
      "Y": -2
    },
    {
      "X": 10,
      "Y": -1
    },
    {
      "X": 11,
      "Y": 0
    },
    {
      "X": 9,
      "Y": -3
    },
    {
      "X": 10,
      "Y": -2
    },
    {
      "X": 11,
      "Y": -1
    },
    {
      "X": 9,
      "Y": -4
    },
    {
      "X": 10,
      "Y": -3
    },
    {
      "X": 11,
      "Y": -2
    },
    {
      "X": 9,
      "Y": -5
    },
    {
      "X": 10,
      "Y": -4
    },
    {
      "X": 11,
      "Y": -3
    },
    {
      "X": 9,
      "Y": -6
    },
    {
      "X": 10,
      "Y": -5
    },
    {
      "X": 11,
      "Y": -4
    },
    {
      "X": 9,
      "Y": -7
    },
    {
      "X": 10,
      "Y": -6
    },
    {
      "X": 11,
      "Y": -5
    },
    {
      "X": 12,
      "Y": -4
    },
    {
      "X": 9,
      "Y": -8
    },
    {
      "X": 10,
      "Y": -7
    },
    {
      "X": 11,
      "Y": -6
    },
    {
      "X": 12,
      "Y": -5
    },
    {
      "X": 13,
      "Y": -4
    },
    {
      "X": 9,
      "Y": -9
    },
    {
      "X": 10,
      "Y": -8
    },
    {
      "X": 11,
      "Y": -7
    },
    {
      "X": 12,
      "Y": -6
    },
    {
      "X": 13,
      "Y": -5
    },
    {
      "X": 14,
      "Y": -4
    },
    {
      "X": 10,
      "Y": -9
    },
    {
      "X": 11,
      "Y": -8
    },
    {
      "X": 12,
      "Y": -7
    },
    {
      "X": 13,
      "Y": -6
    },
    {
      "X": 14,
      "Y": -5
    },
    {
      "X": 11,
      "Y": -9
    },
    {
      "X": 12,
      "Y": -8
    },
    {
      "X": 13,
      "Y": -7
    },
    {
      "X": 14,
      "Y": -6
    },
    {
      "X": 12,
      "Y": -9
    },
    {
      "X": 13,
      "Y": -8
    },
    {
      "X": 14,
      "Y": -7
    },
    {
      "X": 13,
      "Y": -9
    },
    {
      "X": 14,
      "Y": -8
    },
    {
      "X": 14,
      "Y": -9
    },
    {
      "X": 2,
      "Y": 13
    },
    {
      "X": 1,
      "Y": 13
    },
    {
      "X": 0,
      "Y": 13
    }
  ],
  "WallOpenSidesRaw": [
    {
      "X": 16,
      "Y": -6,
      "Side": "North"
    },
    {
      "X": 16,
      "Y": -4,
      "Side": "South"
    },
    {
      "X": 17,
      "Y": -5,
      "Side": "East"
    },
    {
      "X": 6,
      "Y": -5,
      "Side": "West"
    },
    {
      "X": 7,
      "Y": -6,
      "Side": "North"
    },
    {
      "X": 7,
      "Y": -4,
      "Side": "South"
    },
    {
      "X": 6,
      "Y": -11,
      "Side": "West"
    },
    {
      "X": 6,
      "Y": -10,
      "Side": "West"
    },
    {
      "X": 6,
      "Y": -9,
      "Side": "West"
    },
    {
      "X": 6,
      "Y": -8,
      "Side": "West"
    },
    {
      "X": 7,
      "Y": -12,
      "Side": "North"
    },
    {
      "X": 7,
      "Y": -7,
      "Side": "South"
    },
    {
      "X": 8,
      "Y": -11,
      "Side": "East"
    },
    {
      "X": 8,
      "Y": -10,
      "Side": "East"
    },
    {
      "X": 15,
      "Y": -11,
      "Side": "West"
    },
    {
      "X": 15,
      "Y": -10,
      "Side": "West"
    },
    {
      "X": 16,
      "Y": -12,
      "Side": "North"
    },
    {
      "X": 16,
      "Y": -7,
      "Side": "South"
    },
    {
      "X": 17,
      "Y": -11,
      "Side": "East"
    },
    {
      "X": 17,
      "Y": -10,
      "Side": "East"
    },
    {
      "X": 17,
      "Y": -9,
      "Side": "East"
    },
    {
      "X": 17,
      "Y": -8,
      "Side": "East"
    },
    {
      "X": 9,
      "Y": -14,
      "Side": "West"
    },
    {
      "X": 9,
      "Y": -13,
      "Side": "West"
    },
    {
      "X": 9,
      "Y": -12,
      "Side": "West"
    },
    {
      "X": 9,
      "Y": -11,
      "Side": "West"
    },
    {
      "X": 10,
      "Y": -15,
      "Side": "North"
    },
    {
      "X": 11,
      "Y": -14,
      "Side": "East"
    },
    {
      "X": 11,
      "Y": -13,
      "Side": "East"
    },
    {
      "X": 11,
      "Y": -12,
      "Side": "East"
    },
    {
      "X": 11,
      "Y": -11,
      "Side": "East"
    },
    {
      "X": 12,
      "Y": -14,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": -13,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": -12,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": -11,
      "Side": "West"
    },
    {
      "X": 13,
      "Y": -15,
      "Side": "North"
    },
    {
      "X": 14,
      "Y": -14,
      "Side": "East"
    },
    {
      "X": 14,
      "Y": -13,
      "Side": "East"
    },
    {
      "X": 14,
      "Y": -12,
      "Side": "East"
    },
    {
      "X": 14,
      "Y": -11,
      "Side": "East"
    },
    {
      "X": 12,
      "Y": -2,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": 1,
      "Side": "West"
    },
    {
      "X": 13,
      "Y": -3,
      "Side": "North"
    },
    {
      "X": 13,
      "Y": 2,
      "Side": "South"
    },
    {
      "X": 14,
      "Y": -3,
      "Side": "North"
    },
    {
      "X": 14,
      "Y": 2,
      "Side": "South"
    },
    {
      "X": 15,
      "Y": -3,
      "Side": "North"
    },
    {
      "X": 15,
      "Y": 2,
      "Side": "South"
    },
    {
      "X": 16,
      "Y": -3,
      "Side": "North"
    },
    {
      "X": 16,
      "Y": 2,
      "Side": "South"
    },
    {
      "X": 17,
      "Y": -2,
      "Side": "East"
    },
    {
      "X": 17,
      "Y": -1,
      "Side": "East"
    },
    {
      "X": 17,
      "Y": 0,
      "Side": "East"
    },
    {
      "X": 17,
      "Y": 1,
      "Side": "East"
    },
    {
      "X": 12,
      "Y": 4,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": 7,
      "Side": "West"
    },
    {
      "X": 13,
      "Y": 3,
      "Side": "North"
    },
    {
      "X": 13,
      "Y": 8,
      "Side": "South"
    },
    {
      "X": 14,
      "Y": 3,
      "Side": "North"
    },
    {
      "X": 14,
      "Y": 8,
      "Side": "South"
    },
    {
      "X": 15,
      "Y": 3,
      "Side": "North"
    },
    {
      "X": 15,
      "Y": 8,
      "Side": "South"
    },
    {
      "X": 16,
      "Y": 3,
      "Side": "North"
    },
    {
      "X": 16,
      "Y": 8,
      "Side": "South"
    },
    {
      "X": 17,
      "Y": 4,
      "Side": "East"
    },
    {
      "X": 17,
      "Y": 5,
      "Side": "East"
    },
    {
      "X": 17,
      "Y": 6,
      "Side": "East"
    },
    {
      "X": 19,
      "Y": 6,
      "Side": "North"
    },
    {
      "X": 19,
      "Y": 8,
      "Side": "South"
    },
    {
      "X": 9,
      "Y": 10,
      "Side": "West"
    },
    {
      "X": 9,
      "Y": 11,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": 14,
      "Side": "South"
    },
    {
      "X": 12,
      "Y": 9,
      "Side": "North"
    },
    {
      "X": 12,
      "Y": 14,
      "Side": "South"
    },
    {
      "X": 13,
      "Y": 9,
      "Side": "North"
    },
    {
      "X": 14,
      "Y": 10,
      "Side": "East"
    },
    {
      "X": 14,
      "Y": 11,
      "Side": "East"
    },
    {
      "X": 15,
      "Y": 10,
      "Side": "West"
    },
    {
      "X": 15,
      "Y": 11,
      "Side": "West"
    },
    {
      "X": 16,
      "Y": 9,
      "Side": "North"
    },
    {
      "X": 17,
      "Y": 9,
      "Side": "North"
    },
    {
      "X": 17,
      "Y": 14,
      "Side": "South"
    },
    {
      "X": 18,
      "Y": 9,
      "Side": "North"
    },
    {
      "X": 18,
      "Y": 14,
      "Side": "South"
    },
    {
      "X": 19,
      "Y": 9,
      "Side": "North"
    },
    {
      "X": 19,
      "Y": 14,
      "Side": "South"
    },
    {
      "X": 20,
      "Y": 10,
      "Side": "East"
    },
    {
      "X": 20,
      "Y": 11,
      "Side": "East"
    },
    {
      "X": 20,
      "Y": 12,
      "Side": "East"
    },
    {
      "X": 3,
      "Y": 10,
      "Side": "West"
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
      "Y": 9,
      "Side": "North"
    },
    {
      "X": 4,
      "Y": 14,
      "Side": "South"
    },
    {
      "X": 5,
      "Y": 14,
      "Side": "South"
    },
    {
      "X": 6,
      "Y": 14,
      "Side": "South"
    },
    {
      "X": 8,
      "Y": 10,
      "Side": "East"
    },
    {
      "X": 8,
      "Y": 11,
      "Side": "East"
    },
    {
      "X": 6,
      "Y": -2,
      "Side": "West"
    },
    {
      "X": 7,
      "Y": -3,
      "Side": "North"
    },
    {
      "X": 7,
      "Y": -1,
      "Side": "South"
    },
    {
      "X": 8,
      "Y": -2,
      "Side": "East"
    },
    {
      "X": 6,
      "Y": 1,
      "Side": "West"
    },
    {
      "X": 6,
      "Y": 2,
      "Side": "West"
    },
    {
      "X": 6,
      "Y": 3,
      "Side": "West"
    },
    {
      "X": 6,
      "Y": 4,
      "Side": "West"
    },
    {
      "X": 7,
      "Y": 0,
      "Side": "North"
    },
    {
      "X": 7,
      "Y": 5,
      "Side": "South"
    },
    {
      "X": 8,
      "Y": 1,
      "Side": "East"
    },
    {
      "X": 8,
      "Y": 2,
      "Side": "East"
    },
    {
      "X": 22,
      "Y": 12,
      "Side": "North"
    },
    {
      "X": 22,
      "Y": 14,
      "Side": "South"
    },
    {
      "X": 25,
      "Y": 12,
      "Side": "North"
    },
    {
      "X": 26,
      "Y": 13,
      "Side": "East"
    },
    {
      "X": 24,
      "Y": 16,
      "Side": "West"
    },
    {
      "X": 25,
      "Y": 17,
      "Side": "South"
    },
    {
      "X": 26,
      "Y": 16,
      "Side": "East"
    },
    {
      "X": 1,
      "Y": 12,
      "Side": "North"
    },
    {
      "X": 1,
      "Y": 14,
      "Side": "South"
    },
    {
      "X": -3,
      "Y": 13,
      "Side": "West"
    },
    {
      "X": -3,
      "Y": 16,
      "Side": "West"
    },
    {
      "X": -2,
      "Y": 12,
      "Side": "North"
    },
    {
      "X": -2,
      "Y": 17,
      "Side": "South"
    },
    {
      "X": -1,
      "Y": 16,
      "Side": "East"
    },
    {
      "X": 9,
      "Y": 16,
      "Side": "West"
    },
    {
      "X": 9,
      "Y": 18,
      "Side": "West"
    },
    {
      "X": 9,
      "Y": 19,
      "Side": "West"
    },
    {
      "X": 10,
      "Y": 20,
      "Side": "South"
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
      "X": 11,
      "Y": 18,
      "Side": "East"
    },
    {
      "X": 11,
      "Y": 19,
      "Side": "East"
    },
    {
      "X": 12,
      "Y": 16,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": 17,
      "Side": "West"
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
      "X": 13,
      "Y": 20,
      "Side": "South"
    },
    {
      "X": 14,
      "Y": 16,
      "Side": "East"
    },
    {
      "X": 14,
      "Y": 18,
      "Side": "East"
    },
    {
      "X": 14,
      "Y": 19,
      "Side": "East"
    },
    {
      "X": 15,
      "Y": 16,
      "Side": "West"
    },
    {
      "X": 16,
      "Y": 17,
      "Side": "South"
    },
    {
      "X": 17,
      "Y": 16,
      "Side": "East"
    },
    {
      "X": 6,
      "Y": 16,
      "Side": "West"
    },
    {
      "X": 7,
      "Y": 17,
      "Side": "South"
    },
    {
      "X": 8,
      "Y": 16,
      "Side": "East"
    },
    {
      "X": 4,
      "Y": 6,
      "Side": "North"
    },
    {
      "X": 4,
      "Y": 8,
      "Side": "South"
    }
  ],
  "DoorEdgesRaw": [
    {
      "X": 14,
      "Y": -5,
      "Side": "East",
      "Id": "door-edge-0"
    },
    {
      "X": 8,
      "Y": -5,
      "Side": "East",
      "Id": "door-edge-1"
    },
    {
      "X": 14,
      "Y": -8,
      "Side": "East",
      "Id": "door-edge-2"
    },
    {
      "X": 8,
      "Y": -8,
      "Side": "East",
      "Id": "door-edge-3"
    },
    {
      "X": 13,
      "Y": -10,
      "Side": "South",
      "Id": "door-edge-5"
    },
    {
      "X": 10,
      "Y": -10,
      "Side": "South",
      "Id": "door-edge-6"
    },
    {
      "X": 11,
      "Y": -1,
      "Side": "East",
      "Id": "door-edge-7"
    },
    {
      "X": 11,
      "Y": 0,
      "Side": "East",
      "Id": "door-edge-7"
    },
    {
      "X": 10,
      "Y": 8,
      "Side": "South",
      "Id": "door-edge-8"
    },
    {
      "X": 11,
      "Y": 8,
      "Side": "South",
      "Id": "door-edge-8"
    },
    {
      "X": 17,
      "Y": 7,
      "Side": "East",
      "Id": "door-edge-9"
    },
    {
      "X": 11,
      "Y": 5,
      "Side": "East",
      "Id": "door-edge-10"
    },
    {
      "X": 11,
      "Y": 6,
      "Side": "East",
      "Id": "door-edge-10"
    },
    {
      "X": 6,
      "Y": 8,
      "Side": "South",
      "Id": "door-edge-11"
    },
    {
      "X": 7,
      "Y": 8,
      "Side": "South",
      "Id": "door-edge-11"
    },
    {
      "X": 14,
      "Y": 13,
      "Side": "East",
      "Id": "door-edge-14"
    },
    {
      "X": 14,
      "Y": 12,
      "Side": "East",
      "Id": "door-edge-14"
    },
    {
      "X": 8,
      "Y": 12,
      "Side": "East",
      "Id": "door-edge-13"
    },
    {
      "X": 8,
      "Y": 13,
      "Side": "East",
      "Id": "door-edge-13"
    },
    {
      "X": 8,
      "Y": 3,
      "Side": "East",
      "Id": "door-edge-15"
    },
    {
      "X": 8,
      "Y": 4,
      "Side": "East",
      "Id": "door-edge-15"
    },
    {
      "X": 23,
      "Y": 13,
      "Side": "East",
      "Id": "door-edge-17"
    },
    {
      "X": 25,
      "Y": 14,
      "Side": "South",
      "Id": "door-edge-18"
    },
    {
      "X": 20,
      "Y": 13,
      "Side": "East",
      "Id": "door-edge-19"
    },
    {
      "X": -2,
      "Y": 14,
      "Side": "South",
      "Id": "door-edge-20"
    },
    {
      "X": -1,
      "Y": 13,
      "Side": "East",
      "Id": "door-edge-21"
    },
    {
      "X": 7,
      "Y": 14,
      "Side": "South",
      "Id": "door-edge-22"
    },
    {
      "X": 10,
      "Y": 14,
      "Side": "South",
      "Id": "door-edge-23"
    },
    {
      "X": 13,
      "Y": 14,
      "Side": "South",
      "Id": "door-edge-24"
    },
    {
      "X": 16,
      "Y": 14,
      "Side": "South",
      "Id": "door-edge-25"
    },
    {
      "X": 5,
      "Y": 7,
      "Side": "East",
      "Id": "door-edge-26"
    },
    {
      "X": 2,
      "Y": 7,
      "Side": "East",
      "Id": "door-edge-34"
    },
    {
      "X": 20,
      "Y": 7,
      "Side": "East",
      "Id": "door-edge-35"
    },
    {
      "X": 2,
      "Y": 13,
      "Side": "East",
      "Id": "door-edge-29"
    }
  ],
  "WreckPatchesRaw": null,
  "WallMaterials": [],
  "Engines": [
    {
      "X": 7.5,
      "Y": -4.5,
      "Facing": "West",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 16.5,
      "Y": -4.5,
      "Facing": "East",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 25.5,
      "Y": 13.5,
      "Facing": "North",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 25.5,
      "Y": 16.5,
      "Facing": "East",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": -1.5,
      "Y": 16.5,
      "Facing": "West",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": -1.5,
      "Y": 13.5,
      "Facing": "North",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 7.5,
      "Y": 16.5,
      "Facing": "South",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 10.5,
      "Y": 19.5,
      "Facing": "South",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 13.5,
      "Y": 19.5,
      "Facing": "South",
      "MaxThrust": 8,
      "Role": "Marching"
    },
    {
      "X": 16.5,
      "Y": 16.5,
      "Facing": "South",
      "MaxThrust": 8,
      "Role": "Marching"
    }
  ],
  "SupplementalWallTiles": [
    {
      "X": 6,
      "Y": -4
    },
    {
      "X": 6,
      "Y": 0
    },
    {
      "X": 17,
      "Y": -7
    },
    {
      "X": 17,
      "Y": -3
    },
    {
      "X": 3,
      "Y": 9
    },
    {
      "X": 6,
      "Y": -7
    },
    {
      "X": 6,
      "Y": -3
    },
    {
      "X": 15,
      "Y": -10
    },
    {
      "X": 15,
      "Y": -11
    },
    {
      "X": 15,
      "Y": -12
    },
    {
      "X": 17,
      "Y": -6
    },
    {
      "X": 8,
      "Y": -10
    },
    {
      "X": 8,
      "Y": -11
    },
    {
      "X": 8,
      "Y": -12
    },
    {
      "X": 6,
      "Y": -6
    },
    {
      "X": 11,
      "Y": -15
    },
    {
      "X": 12,
      "Y": -15
    },
    {
      "X": 17,
      "Y": 3
    },
    {
      "X": 17,
      "Y": -4
    },
    {
      "X": 20,
      "Y": 9
    },
    {
      "X": 17,
      "Y": 6
    },
    {
      "X": 17,
      "Y": 5
    },
    {
      "X": 17,
      "Y": 4
    },
    {
      "X": 17,
      "Y": 8
    },
    {
      "X": 14,
      "Y": 9
    },
    {
      "X": 15,
      "Y": 9
    },
    {
      "X": 17,
      "Y": 2
    },
    {
      "X": 5,
      "Y": 9
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
      "X": 4,
      "Y": 9
    },
    {
      "X": 3,
      "Y": 10
    },
    {
      "X": 6,
      "Y": 14
    },
    {
      "X": 3,
      "Y": 11
    },
    {
      "X": 3,
      "Y": 12
    },
    {
      "X": 8,
      "Y": 14
    },
    {
      "X": 3,
      "Y": 14
    },
    {
      "X": 15,
      "Y": 11
    },
    {
      "X": 15,
      "Y": 14
    },
    {
      "X": 17,
      "Y": 14
    },
    {
      "X": 15,
      "Y": 10
    },
    {
      "X": 18,
      "Y": 14
    },
    {
      "X": 20,
      "Y": 12
    },
    {
      "X": 19,
      "Y": 14
    },
    {
      "X": 20,
      "Y": 11
    },
    {
      "X": 20,
      "Y": 10
    },
    {
      "X": 9,
      "Y": 11
    },
    {
      "X": 9,
      "Y": 14
    },
    {
      "X": 11,
      "Y": 14
    },
    {
      "X": 9,
      "Y": 10
    },
    {
      "X": 12,
      "Y": 14
    },
    {
      "X": 12,
      "Y": 9
    },
    {
      "X": 14,
      "Y": 11
    },
    {
      "X": 14,
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
      "X": 6,
      "Y": -1
    },
    {
      "X": 24,
      "Y": 14
    },
    {
      "X": 26,
      "Y": 14
    },
    {
      "X": -1,
      "Y": 14
    },
    {
      "X": -3,
      "Y": 14
    },
    {
      "X": 8,
      "Y": 17
    },
    {
      "X": 12,
      "Y": 20
    },
    {
      "X": 15,
      "Y": 17
    },
    {
      "X": 11,
      "Y": 20
    },
    {
      "X": 6,
      "Y": 5
    },
    {
      "X": 12,
      "Y": 8
    },
    {
      "X": 12,
      "Y": 3
    },
    {
      "X": 8,
      "Y": 0
    },
    {
      "X": 12,
      "Y": 2
    },
    {
      "X": 8,
      "Y": -1
    },
    {
      "X": 8,
      "Y": -3
    },
    {
      "X": 12,
      "Y": -3
    },
    {
      "X": 16,
      "Y": -4
    },
    {
      "X": 7,
      "Y": -4
    },
    {
      "X": 8,
      "Y": -4
    },
    {
      "X": 8,
      "Y": -9
    },
    {
      "X": 8,
      "Y": -7
    },
    {
      "X": 15,
      "Y": -9
    },
    {
      "X": 15,
      "Y": -7
    },
    {
      "X": 11,
      "Y": -14
    },
    {
      "X": 11,
      "Y": -13
    },
    {
      "X": 11,
      "Y": -12
    },
    {
      "X": 11,
      "Y": -11
    },
    {
      "X": 11,
      "Y": -10
    },
    {
      "X": 12,
      "Y": -2
    },
    {
      "X": 12,
      "Y": 1
    },
    {
      "X": 14,
      "Y": 2
    },
    {
      "X": 15,
      "Y": 2
    },
    {
      "X": 16,
      "Y": 2
    },
    {
      "X": 12,
      "Y": 4
    },
    {
      "X": 12,
      "Y": 7
    },
    {
      "X": 19,
      "Y": 8
    },
    {
      "X": 9,
      "Y": 9
    },
    {
      "X": 8,
      "Y": 9
    },
    {
      "X": 7,
      "Y": -1
    },
    {
      "X": 8,
      "Y": -2
    },
    {
      "X": 7,
      "Y": 5
    },
    {
      "X": 8,
      "Y": 1
    },
    {
      "X": 8,
      "Y": 2
    },
    {
      "X": 8,
      "Y": 5
    },
    {
      "X": 11,
      "Y": 16
    },
    {
      "X": 11,
      "Y": 17
    },
    {
      "X": 11,
      "Y": 18
    },
    {
      "X": 11,
      "Y": 19
    },
    {
      "X": 14,
      "Y": 16
    },
    {
      "X": 14,
      "Y": 17
    },
    {
      "X": 8,
      "Y": 16
    }
  ],
  "ForcedFloorTiles": [
    {
      "X": 7,
      "Y": -2
    },
    {
      "X": 15,
      "Y": -5
    },
    {
      "X": 16,
      "Y": -5
    },
    {
      "X": 3,
      "Y": 7
    },
    {
      "X": 4,
      "Y": 7
    },
    {
      "X": 5,
      "Y": 7
    },
    {
      "X": 8,
      "Y": -5
    },
    {
      "X": 7,
      "Y": -5
    },
    {
      "X": 15,
      "Y": -8
    },
    {
      "X": 16,
      "Y": -8
    },
    {
      "X": 16,
      "Y": -9
    },
    {
      "X": 16,
      "Y": -10
    },
    {
      "X": 16,
      "Y": -11
    },
    {
      "X": 8,
      "Y": -8
    },
    {
      "X": 7,
      "Y": -8
    },
    {
      "X": 7,
      "Y": -9
    },
    {
      "X": 7,
      "Y": -10
    },
    {
      "X": 7,
      "Y": -11
    },
    {
      "X": 13,
      "Y": -10
    },
    {
      "X": 13,
      "Y": -11
    },
    {
      "X": 13,
      "Y": -12
    },
    {
      "X": 13,
      "Y": -13
    },
    {
      "X": 13,
      "Y": -14
    },
    {
      "X": 10,
      "Y": -10
    },
    {
      "X": 10,
      "Y": -11
    },
    {
      "X": 10,
      "Y": -12
    },
    {
      "X": 10,
      "Y": -13
    },
    {
      "X": 10,
      "Y": -14
    },
    {
      "X": 12,
      "Y": 0
    },
    {
      "X": 12,
      "Y": -1
    },
    {
      "X": 13,
      "Y": 0
    },
    {
      "X": 13,
      "Y": -1
    },
    {
      "X": 13,
      "Y": 1
    },
    {
      "X": 14,
      "Y": 0
    },
    {
      "X": 13,
      "Y": -2
    },
    {
      "X": 14,
      "Y": -1
    },
    {
      "X": 14,
      "Y": 1
    },
    {
      "X": 15,
      "Y": 0
    },
    {
      "X": 14,
      "Y": -2
    },
    {
      "X": 15,
      "Y": -1
    },
    {
      "X": 15,
      "Y": 1
    },
    {
      "X": 16,
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
      "X": 16,
      "Y": 1
    },
    {
      "X": 16,
      "Y": -2
    },
    {
      "X": 18,
      "Y": 7
    },
    {
      "X": 19,
      "Y": 7
    },
    {
      "X": 20,
      "Y": 7
    },
    {
      "X": 12,
      "Y": 6
    },
    {
      "X": 12,
      "Y": 5
    },
    {
      "X": 13,
      "Y": 6
    },
    {
      "X": 13,
      "Y": 5
    },
    {
      "X": 13,
      "Y": 7
    },
    {
      "X": 14,
      "Y": 6
    },
    {
      "X": 13,
      "Y": 4
    },
    {
      "X": 14,
      "Y": 5
    },
    {
      "X": 14,
      "Y": 7
    },
    {
      "X": 15,
      "Y": 6
    },
    {
      "X": 14,
      "Y": 4
    },
    {
      "X": 15,
      "Y": 5
    },
    {
      "X": 15,
      "Y": 7
    },
    {
      "X": 16,
      "Y": 6
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
      "X": 16,
      "Y": 7
    },
    {
      "X": 16,
      "Y": 4
    },
    {
      "X": 17,
      "Y": 7
    },
    {
      "X": 6,
      "Y": 9
    },
    {
      "X": 6,
      "Y": 10
    },
    {
      "X": 7,
      "Y": 9
    },
    {
      "X": 6,
      "Y": 11
    },
    {
      "X": 7,
      "Y": 10
    },
    {
      "X": 5,
      "Y": 10
    },
    {
      "X": 6,
      "Y": 12
    },
    {
      "X": 7,
      "Y": 11
    },
    {
      "X": 5,
      "Y": 11
    },
    {
      "X": 4,
      "Y": 10
    },
    {
      "X": 6,
      "Y": 13
    },
    {
      "X": 7,
      "Y": 12
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
      "X": 7,
      "Y": 13
    },
    {
      "X": 5,
      "Y": 13
    },
    {
      "X": 8,
      "Y": 12
    },
    {
      "X": 4,
      "Y": 12
    },
    {
      "X": 7,
      "Y": 14
    },
    {
      "X": 8,
      "Y": 13
    },
    {
      "X": 4,
      "Y": 13
    },
    {
      "X": 3,
      "Y": 13
    },
    {
      "X": 15,
      "Y": 12
    },
    {
      "X": 15,
      "Y": 13
    },
    {
      "X": 16,
      "Y": 12
    },
    {
      "X": 16,
      "Y": 13
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
      "X": 16,
      "Y": 14
    },
    {
      "X": 17,
      "Y": 13
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
      "X": 18,
      "Y": 13
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
      "X": 19,
      "Y": 13
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
      "X": 20,
      "Y": 13
    },
    {
      "X": 19,
      "Y": 10
    },
    {
      "X": 9,
      "Y": 12
    },
    {
      "X": 9,
      "Y": 13
    },
    {
      "X": 10,
      "Y": 12
    },
    {
      "X": 10,
      "Y": 13
    },
    {
      "X": 10,
      "Y": 11
    },
    {
      "X": 11,
      "Y": 12
    },
    {
      "X": 10,
      "Y": 14
    },
    {
      "X": 11,
      "Y": 13
    },
    {
      "X": 10,
      "Y": 10
    },
    {
      "X": 11,
      "Y": 11
    },
    {
      "X": 12,
      "Y": 12
    },
    {
      "X": 12,
      "Y": 13
    },
    {
      "X": 10,
      "Y": 9
    },
    {
      "X": 11,
      "Y": 10
    },
    {
      "X": 12,
      "Y": 11
    },
    {
      "X": 13,
      "Y": 12
    },
    {
      "X": 13,
      "Y": 13
    },
    {
      "X": 11,
      "Y": 9
    },
    {
      "X": 12,
      "Y": 10
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
      "X": 13,
      "Y": 14
    },
    {
      "X": 14,
      "Y": 13
    },
    {
      "X": 13,
      "Y": 10
    },
    {
      "X": 8,
      "Y": 4
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
      "X": 7,
      "Y": 3
    },
    {
      "X": 7,
      "Y": 2
    },
    {
      "X": 7,
      "Y": 1
    },
    {
      "X": 24,
      "Y": 13
    },
    {
      "X": 25,
      "Y": 13
    },
    {
      "X": 25,
      "Y": 14
    },
    {
      "X": 25,
      "Y": 15
    },
    {
      "X": 25,
      "Y": 16
    },
    {
      "X": 21,
      "Y": 13
    },
    {
      "X": 22,
      "Y": 13
    },
    {
      "X": 23,
      "Y": 13
    },
    {
      "X": -2,
      "Y": 15
    },
    {
      "X": -2,
      "Y": 16
    },
    {
      "X": -1,
      "Y": 13
    },
    {
      "X": -2,
      "Y": 13
    },
    {
      "X": -2,
      "Y": 14
    },
    {
      "X": 7,
      "Y": 15
    },
    {
      "X": 7,
      "Y": 16
    },
    {
      "X": 10,
      "Y": 15
    },
    {
      "X": 10,
      "Y": 16
    },
    {
      "X": 10,
      "Y": 17
    },
    {
      "X": 10,
      "Y": 18
    },
    {
      "X": 10,
      "Y": 19
    },
    {
      "X": 13,
      "Y": 15
    },
    {
      "X": 13,
      "Y": 16
    },
    {
      "X": 13,
      "Y": 17
    },
    {
      "X": 13,
      "Y": 18
    },
    {
      "X": 13,
      "Y": 19
    },
    {
      "X": 16,
      "Y": 15
    },
    {
      "X": 16,
      "Y": 16
    },
    {
      "X": 6,
      "Y": 7
    },
    {
      "X": 6,
      "Y": 6
    },
    {
      "X": 6,
      "Y": 8
    },
    {
      "X": 7,
      "Y": 7
    },
    {
      "X": 7,
      "Y": 6
    },
    {
      "X": 7,
      "Y": 8
    },
    {
      "X": 8,
      "Y": 7
    },
    {
      "X": 8,
      "Y": 6
    },
    {
      "X": 8,
      "Y": 8
    },
    {
      "X": 9,
      "Y": 7
    },
    {
      "X": 9,
      "Y": 6
    },
    {
      "X": 9,
      "Y": 8
    },
    {
      "X": 10,
      "Y": 7
    },
    {
      "X": 9,
      "Y": 5
    },
    {
      "X": 10,
      "Y": 6
    },
    {
      "X": 10,
      "Y": 8
    },
    {
      "X": 11,
      "Y": 7
    },
    {
      "X": 9,
      "Y": 4
    },
    {
      "X": 10,
      "Y": 5
    },
    {
      "X": 11,
      "Y": 6
    },
    {
      "X": 11,
      "Y": 8
    },
    {
      "X": 9,
      "Y": 3
    },
    {
      "X": 10,
      "Y": 4
    },
    {
      "X": 11,
      "Y": 5
    },
    {
      "X": 9,
      "Y": 2
    },
    {
      "X": 10,
      "Y": 3
    },
    {
      "X": 11,
      "Y": 4
    },
    {
      "X": 9,
      "Y": 1
    },
    {
      "X": 10,
      "Y": 2
    },
    {
      "X": 11,
      "Y": 3
    },
    {
      "X": 9,
      "Y": 0
    },
    {
      "X": 10,
      "Y": 1
    },
    {
      "X": 11,
      "Y": 2
    },
    {
      "X": 9,
      "Y": -1
    },
    {
      "X": 10,
      "Y": 0
    },
    {
      "X": 11,
      "Y": 1
    },
    {
      "X": 9,
      "Y": -2
    },
    {
      "X": 10,
      "Y": -1
    },
    {
      "X": 11,
      "Y": 0
    },
    {
      "X": 9,
      "Y": -3
    },
    {
      "X": 10,
      "Y": -2
    },
    {
      "X": 11,
      "Y": -1
    },
    {
      "X": 9,
      "Y": -4
    },
    {
      "X": 10,
      "Y": -3
    },
    {
      "X": 11,
      "Y": -2
    },
    {
      "X": 9,
      "Y": -5
    },
    {
      "X": 10,
      "Y": -4
    },
    {
      "X": 11,
      "Y": -3
    },
    {
      "X": 9,
      "Y": -6
    },
    {
      "X": 10,
      "Y": -5
    },
    {
      "X": 11,
      "Y": -4
    },
    {
      "X": 9,
      "Y": -7
    },
    {
      "X": 10,
      "Y": -6
    },
    {
      "X": 11,
      "Y": -5
    },
    {
      "X": 12,
      "Y": -4
    },
    {
      "X": 9,
      "Y": -8
    },
    {
      "X": 10,
      "Y": -7
    },
    {
      "X": 11,
      "Y": -6
    },
    {
      "X": 12,
      "Y": -5
    },
    {
      "X": 13,
      "Y": -4
    },
    {
      "X": 9,
      "Y": -9
    },
    {
      "X": 10,
      "Y": -8
    },
    {
      "X": 11,
      "Y": -7
    },
    {
      "X": 12,
      "Y": -6
    },
    {
      "X": 13,
      "Y": -5
    },
    {
      "X": 14,
      "Y": -4
    },
    {
      "X": 10,
      "Y": -9
    },
    {
      "X": 11,
      "Y": -8
    },
    {
      "X": 12,
      "Y": -7
    },
    {
      "X": 13,
      "Y": -6
    },
    {
      "X": 14,
      "Y": -5
    },
    {
      "X": 11,
      "Y": -9
    },
    {
      "X": 12,
      "Y": -8
    },
    {
      "X": 13,
      "Y": -7
    },
    {
      "X": 14,
      "Y": -6
    },
    {
      "X": 12,
      "Y": -9
    },
    {
      "X": 13,
      "Y": -8
    },
    {
      "X": 14,
      "Y": -7
    },
    {
      "X": 13,
      "Y": -9
    },
    {
      "X": 14,
      "Y": -8
    },
    {
      "X": 14,
      "Y": -9
    },
    {
      "X": 2,
      "Y": 13
    },
    {
      "X": 1,
      "Y": 13
    },
    {
      "X": 0,
      "Y": 13
    }
  ],
  "WallOpenSides": [
    {
      "X": 16,
      "Y": -6,
      "Side": "North"
    },
    {
      "X": 16,
      "Y": -4,
      "Side": "South"
    },
    {
      "X": 17,
      "Y": -5,
      "Side": "East"
    },
    {
      "X": 6,
      "Y": -5,
      "Side": "West"
    },
    {
      "X": 7,
      "Y": -6,
      "Side": "North"
    },
    {
      "X": 7,
      "Y": -4,
      "Side": "South"
    },
    {
      "X": 6,
      "Y": -11,
      "Side": "West"
    },
    {
      "X": 6,
      "Y": -10,
      "Side": "West"
    },
    {
      "X": 6,
      "Y": -9,
      "Side": "West"
    },
    {
      "X": 6,
      "Y": -8,
      "Side": "West"
    },
    {
      "X": 7,
      "Y": -12,
      "Side": "North"
    },
    {
      "X": 7,
      "Y": -7,
      "Side": "South"
    },
    {
      "X": 8,
      "Y": -11,
      "Side": "East"
    },
    {
      "X": 8,
      "Y": -10,
      "Side": "East"
    },
    {
      "X": 15,
      "Y": -11,
      "Side": "West"
    },
    {
      "X": 15,
      "Y": -10,
      "Side": "West"
    },
    {
      "X": 16,
      "Y": -12,
      "Side": "North"
    },
    {
      "X": 16,
      "Y": -7,
      "Side": "South"
    },
    {
      "X": 17,
      "Y": -11,
      "Side": "East"
    },
    {
      "X": 17,
      "Y": -10,
      "Side": "East"
    },
    {
      "X": 17,
      "Y": -9,
      "Side": "East"
    },
    {
      "X": 17,
      "Y": -8,
      "Side": "East"
    },
    {
      "X": 9,
      "Y": -14,
      "Side": "West"
    },
    {
      "X": 9,
      "Y": -13,
      "Side": "West"
    },
    {
      "X": 9,
      "Y": -12,
      "Side": "West"
    },
    {
      "X": 9,
      "Y": -11,
      "Side": "West"
    },
    {
      "X": 10,
      "Y": -15,
      "Side": "North"
    },
    {
      "X": 11,
      "Y": -14,
      "Side": "East"
    },
    {
      "X": 11,
      "Y": -13,
      "Side": "East"
    },
    {
      "X": 11,
      "Y": -12,
      "Side": "East"
    },
    {
      "X": 11,
      "Y": -11,
      "Side": "East"
    },
    {
      "X": 12,
      "Y": -14,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": -13,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": -12,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": -11,
      "Side": "West"
    },
    {
      "X": 13,
      "Y": -15,
      "Side": "North"
    },
    {
      "X": 14,
      "Y": -14,
      "Side": "East"
    },
    {
      "X": 14,
      "Y": -13,
      "Side": "East"
    },
    {
      "X": 14,
      "Y": -12,
      "Side": "East"
    },
    {
      "X": 14,
      "Y": -11,
      "Side": "East"
    },
    {
      "X": 12,
      "Y": -2,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": 1,
      "Side": "West"
    },
    {
      "X": 13,
      "Y": -3,
      "Side": "North"
    },
    {
      "X": 13,
      "Y": 2,
      "Side": "South"
    },
    {
      "X": 14,
      "Y": -3,
      "Side": "North"
    },
    {
      "X": 14,
      "Y": 2,
      "Side": "South"
    },
    {
      "X": 15,
      "Y": -3,
      "Side": "North"
    },
    {
      "X": 15,
      "Y": 2,
      "Side": "South"
    },
    {
      "X": 16,
      "Y": -3,
      "Side": "North"
    },
    {
      "X": 16,
      "Y": 2,
      "Side": "South"
    },
    {
      "X": 17,
      "Y": -2,
      "Side": "East"
    },
    {
      "X": 17,
      "Y": -1,
      "Side": "East"
    },
    {
      "X": 17,
      "Y": 0,
      "Side": "East"
    },
    {
      "X": 17,
      "Y": 1,
      "Side": "East"
    },
    {
      "X": 12,
      "Y": 4,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": 7,
      "Side": "West"
    },
    {
      "X": 13,
      "Y": 3,
      "Side": "North"
    },
    {
      "X": 13,
      "Y": 8,
      "Side": "South"
    },
    {
      "X": 14,
      "Y": 3,
      "Side": "North"
    },
    {
      "X": 14,
      "Y": 8,
      "Side": "South"
    },
    {
      "X": 15,
      "Y": 3,
      "Side": "North"
    },
    {
      "X": 15,
      "Y": 8,
      "Side": "South"
    },
    {
      "X": 16,
      "Y": 3,
      "Side": "North"
    },
    {
      "X": 16,
      "Y": 8,
      "Side": "South"
    },
    {
      "X": 17,
      "Y": 4,
      "Side": "East"
    },
    {
      "X": 17,
      "Y": 5,
      "Side": "East"
    },
    {
      "X": 17,
      "Y": 6,
      "Side": "East"
    },
    {
      "X": 19,
      "Y": 6,
      "Side": "North"
    },
    {
      "X": 19,
      "Y": 8,
      "Side": "South"
    },
    {
      "X": 9,
      "Y": 10,
      "Side": "West"
    },
    {
      "X": 9,
      "Y": 11,
      "Side": "West"
    },
    {
      "X": 11,
      "Y": 14,
      "Side": "South"
    },
    {
      "X": 12,
      "Y": 9,
      "Side": "North"
    },
    {
      "X": 12,
      "Y": 14,
      "Side": "South"
    },
    {
      "X": 13,
      "Y": 9,
      "Side": "North"
    },
    {
      "X": 14,
      "Y": 10,
      "Side": "East"
    },
    {
      "X": 14,
      "Y": 11,
      "Side": "East"
    },
    {
      "X": 15,
      "Y": 10,
      "Side": "West"
    },
    {
      "X": 15,
      "Y": 11,
      "Side": "West"
    },
    {
      "X": 16,
      "Y": 9,
      "Side": "North"
    },
    {
      "X": 17,
      "Y": 9,
      "Side": "North"
    },
    {
      "X": 17,
      "Y": 14,
      "Side": "South"
    },
    {
      "X": 18,
      "Y": 9,
      "Side": "North"
    },
    {
      "X": 18,
      "Y": 14,
      "Side": "South"
    },
    {
      "X": 19,
      "Y": 9,
      "Side": "North"
    },
    {
      "X": 19,
      "Y": 14,
      "Side": "South"
    },
    {
      "X": 20,
      "Y": 10,
      "Side": "East"
    },
    {
      "X": 20,
      "Y": 11,
      "Side": "East"
    },
    {
      "X": 20,
      "Y": 12,
      "Side": "East"
    },
    {
      "X": 3,
      "Y": 10,
      "Side": "West"
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
      "Y": 9,
      "Side": "North"
    },
    {
      "X": 4,
      "Y": 14,
      "Side": "South"
    },
    {
      "X": 5,
      "Y": 14,
      "Side": "South"
    },
    {
      "X": 6,
      "Y": 14,
      "Side": "South"
    },
    {
      "X": 8,
      "Y": 10,
      "Side": "East"
    },
    {
      "X": 8,
      "Y": 11,
      "Side": "East"
    },
    {
      "X": 6,
      "Y": -2,
      "Side": "West"
    },
    {
      "X": 7,
      "Y": -3,
      "Side": "North"
    },
    {
      "X": 7,
      "Y": -1,
      "Side": "South"
    },
    {
      "X": 8,
      "Y": -2,
      "Side": "East"
    },
    {
      "X": 6,
      "Y": 1,
      "Side": "West"
    },
    {
      "X": 6,
      "Y": 2,
      "Side": "West"
    },
    {
      "X": 6,
      "Y": 3,
      "Side": "West"
    },
    {
      "X": 6,
      "Y": 4,
      "Side": "West"
    },
    {
      "X": 7,
      "Y": 0,
      "Side": "North"
    },
    {
      "X": 7,
      "Y": 5,
      "Side": "South"
    },
    {
      "X": 8,
      "Y": 1,
      "Side": "East"
    },
    {
      "X": 8,
      "Y": 2,
      "Side": "East"
    },
    {
      "X": 22,
      "Y": 12,
      "Side": "North"
    },
    {
      "X": 22,
      "Y": 14,
      "Side": "South"
    },
    {
      "X": 25,
      "Y": 12,
      "Side": "North"
    },
    {
      "X": 26,
      "Y": 13,
      "Side": "East"
    },
    {
      "X": 24,
      "Y": 16,
      "Side": "West"
    },
    {
      "X": 25,
      "Y": 17,
      "Side": "South"
    },
    {
      "X": 26,
      "Y": 16,
      "Side": "East"
    },
    {
      "X": 1,
      "Y": 12,
      "Side": "North"
    },
    {
      "X": 1,
      "Y": 14,
      "Side": "South"
    },
    {
      "X": -3,
      "Y": 13,
      "Side": "West"
    },
    {
      "X": -3,
      "Y": 16,
      "Side": "West"
    },
    {
      "X": -2,
      "Y": 12,
      "Side": "North"
    },
    {
      "X": -2,
      "Y": 17,
      "Side": "South"
    },
    {
      "X": -1,
      "Y": 16,
      "Side": "East"
    },
    {
      "X": 9,
      "Y": 16,
      "Side": "West"
    },
    {
      "X": 9,
      "Y": 18,
      "Side": "West"
    },
    {
      "X": 9,
      "Y": 19,
      "Side": "West"
    },
    {
      "X": 10,
      "Y": 20,
      "Side": "South"
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
      "X": 11,
      "Y": 18,
      "Side": "East"
    },
    {
      "X": 11,
      "Y": 19,
      "Side": "East"
    },
    {
      "X": 12,
      "Y": 16,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": 17,
      "Side": "West"
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
      "X": 13,
      "Y": 20,
      "Side": "South"
    },
    {
      "X": 14,
      "Y": 16,
      "Side": "East"
    },
    {
      "X": 14,
      "Y": 18,
      "Side": "East"
    },
    {
      "X": 14,
      "Y": 19,
      "Side": "East"
    },
    {
      "X": 15,
      "Y": 16,
      "Side": "West"
    },
    {
      "X": 16,
      "Y": 17,
      "Side": "South"
    },
    {
      "X": 17,
      "Y": 16,
      "Side": "East"
    },
    {
      "X": 6,
      "Y": 16,
      "Side": "West"
    },
    {
      "X": 7,
      "Y": 17,
      "Side": "South"
    },
    {
      "X": 8,
      "Y": 16,
      "Side": "East"
    },
    {
      "X": 4,
      "Y": 6,
      "Side": "North"
    },
    {
      "X": 4,
      "Y": 8,
      "Side": "South"
    }
  ],
  "DoorEdges": [
    {
      "X": 14,
      "Y": -5,
      "Side": "East",
      "Id": "door-edge-0"
    },
    {
      "X": 8,
      "Y": -5,
      "Side": "East",
      "Id": "door-edge-1"
    },
    {
      "X": 14,
      "Y": -8,
      "Side": "East",
      "Id": "door-edge-2"
    },
    {
      "X": 8,
      "Y": -8,
      "Side": "East",
      "Id": "door-edge-3"
    },
    {
      "X": 13,
      "Y": -10,
      "Side": "South",
      "Id": "door-edge-5"
    },
    {
      "X": 10,
      "Y": -10,
      "Side": "South",
      "Id": "door-edge-6"
    },
    {
      "X": 11,
      "Y": -1,
      "Side": "East",
      "Id": "door-edge-7"
    },
    {
      "X": 11,
      "Y": 0,
      "Side": "East",
      "Id": "door-edge-7"
    },
    {
      "X": 10,
      "Y": 8,
      "Side": "South",
      "Id": "door-edge-8"
    },
    {
      "X": 11,
      "Y": 8,
      "Side": "South",
      "Id": "door-edge-8"
    },
    {
      "X": 17,
      "Y": 7,
      "Side": "East",
      "Id": "door-edge-9"
    },
    {
      "X": 11,
      "Y": 5,
      "Side": "East",
      "Id": "door-edge-10"
    },
    {
      "X": 11,
      "Y": 6,
      "Side": "East",
      "Id": "door-edge-10"
    },
    {
      "X": 6,
      "Y": 8,
      "Side": "South",
      "Id": "door-edge-11"
    },
    {
      "X": 7,
      "Y": 8,
      "Side": "South",
      "Id": "door-edge-11"
    },
    {
      "X": 14,
      "Y": 13,
      "Side": "East",
      "Id": "door-edge-14"
    },
    {
      "X": 14,
      "Y": 12,
      "Side": "East",
      "Id": "door-edge-14"
    },
    {
      "X": 8,
      "Y": 12,
      "Side": "East",
      "Id": "door-edge-13"
    },
    {
      "X": 8,
      "Y": 13,
      "Side": "East",
      "Id": "door-edge-13"
    },
    {
      "X": 8,
      "Y": 3,
      "Side": "East",
      "Id": "door-edge-15"
    },
    {
      "X": 8,
      "Y": 4,
      "Side": "East",
      "Id": "door-edge-15"
    },
    {
      "X": 23,
      "Y": 13,
      "Side": "East",
      "Id": "door-edge-17"
    },
    {
      "X": 25,
      "Y": 14,
      "Side": "South",
      "Id": "door-edge-18"
    },
    {
      "X": 20,
      "Y": 13,
      "Side": "East",
      "Id": "door-edge-19"
    },
    {
      "X": -2,
      "Y": 14,
      "Side": "South",
      "Id": "door-edge-20"
    },
    {
      "X": -1,
      "Y": 13,
      "Side": "East",
      "Id": "door-edge-21"
    },
    {
      "X": 7,
      "Y": 14,
      "Side": "South",
      "Id": "door-edge-22"
    },
    {
      "X": 10,
      "Y": 14,
      "Side": "South",
      "Id": "door-edge-23"
    },
    {
      "X": 13,
      "Y": 14,
      "Side": "South",
      "Id": "door-edge-24"
    },
    {
      "X": 16,
      "Y": 14,
      "Side": "South",
      "Id": "door-edge-25"
    },
    {
      "X": 5,
      "Y": 7,
      "Side": "East",
      "Id": "door-edge-26"
    },
    {
      "X": 2,
      "Y": 7,
      "Side": "East",
      "Id": "door-edge-34"
    },
    {
      "X": 20,
      "Y": 7,
      "Side": "East",
      "Id": "door-edge-35"
    },
    {
      "X": 2,
      "Y": 13,
      "Side": "East",
      "Id": "door-edge-29"
    }
  ],
  "WreckPatches": []
}
""";
}
