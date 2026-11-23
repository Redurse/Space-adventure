namespace Anabiosis.Shared.Model;

// The outpost the author put together in the Ship Editor from the outpost compartments (the saved design "аванпост1"), frozen as plain JSON the way
// ShipBuiltInFleet freezes ships - the start station's layout (Station.OutpostStart.cs). Verbatim from %LocalAppData%\Anabiosis\custom-ships\.
public sealed partial class Station
{
    private const string OutpostStartJson = """
{
  "Name": "\u0420\u0435\u0430\u043A\u0442\u043E\u0440\u043D\u044B\u0439 \u043E\u0442\u0441\u0435\u043A",
  "Rooms": [
    {
      "Id": "compartment-0",
      "Name": "\u0410\u0434\u043C\u0438\u043D\u0438\u0441\u0442\u0440\u0430\u0446\u0438\u044F \u0430\u0432\u0430\u043D\u043F\u043E\u0441\u0442\u0430",
      "Rects": [
        {
          "X": 13,
          "Y": 2,
          "Width": 8,
          "Height": 10,
          "Left": 13,
          "Right": 21,
          "Top": 2,
          "Bottom": 12,
          "Area": 80,
          "Center": {
            "X": 17,
            "Y": 7
          }
        },
        {
          "X": 12,
          "Y": 4,
          "Width": 1,
          "Height": 6,
          "Left": 12,
          "Right": 13,
          "Top": 4,
          "Bottom": 10,
          "Area": 6,
          "Center": {
            "X": 12.5,
            "Y": 7
          }
        },
        {
          "X": 21,
          "Y": 4,
          "Width": 1,
          "Height": 6,
          "Left": 21,
          "Right": 22,
          "Top": 4,
          "Bottom": 10,
          "Area": 6,
          "Center": {
            "X": 21.5,
            "Y": 7
          }
        }
      ],
      "X": 12,
      "Y": 2,
      "Width": 10,
      "Height": 10
    },
    {
      "Id": "compartment-1",
      "Name": "\u041F\u0435\u0440\u0435\u0445\u043E\u0434\u043D\u044B\u0439 \u043E\u0442\u0441\u0435\u043A \u0430\u0432\u0430\u043D\u043F\u043E\u0441\u0442\u0430",
      "Rects": [
        {
          "X": 24,
          "Y": 2,
          "Width": 6,
          "Height": 10,
          "Left": 24,
          "Right": 30,
          "Top": 2,
          "Bottom": 12,
          "Area": 60,
          "Center": {
            "X": 27,
            "Y": 7
          }
        },
        {
          "X": 22,
          "Y": 4,
          "Width": 2,
          "Height": 6,
          "Left": 22,
          "Right": 24,
          "Top": 4,
          "Bottom": 10,
          "Area": 12,
          "Center": {
            "X": 23,
            "Y": 7
          }
        },
        {
          "X": 30,
          "Y": 4,
          "Width": 2,
          "Height": 6,
          "Left": 30,
          "Right": 32,
          "Top": 4,
          "Bottom": 10,
          "Area": 12,
          "Center": {
            "X": 31,
            "Y": 7
          }
        }
      ],
      "X": 22,
      "Y": 2,
      "Width": 10,
      "Height": 10
    },
    {
      "Id": "compartment-2",
      "Name": "\u0420\u0435\u0430\u043A\u0442\u043E\u0440\u043D\u044B\u0439 \u043E\u0442\u0441\u0435\u043A \u0430\u0432\u0430\u043D\u043F\u043E\u0441\u0442\u0430",
      "Rects": [
        {
          "X": 22,
          "Y": 13,
          "Width": 10,
          "Height": 8,
          "Left": 22,
          "Right": 32,
          "Top": 13,
          "Bottom": 21,
          "Area": 80,
          "Center": {
            "X": 27,
            "Y": 17
          }
        },
        {
          "X": 24,
          "Y": 12,
          "Width": 6,
          "Height": 1,
          "Left": 24,
          "Right": 30,
          "Top": 12,
          "Bottom": 13,
          "Area": 6,
          "Center": {
            "X": 27,
            "Y": 12.5
          }
        },
        {
          "X": 24,
          "Y": 21,
          "Width": 6,
          "Height": 1,
          "Left": 24,
          "Right": 30,
          "Top": 21,
          "Bottom": 22,
          "Area": 6,
          "Center": {
            "X": 27,
            "Y": 21.5
          }
        }
      ],
      "X": 22,
      "Y": 12,
      "Width": 10,
      "Height": 10
    },
    {
      "Id": "compartment-9",
      "Name": "\u0418\u043D\u0436\u0438\u043D\u0435\u0440\u043D\u044B\u0439 \u043E\u0442\u0441\u0435\u043A \u0430\u0432\u0430\u043D\u043F\u043E\u0441\u0442\u0430",
      "Rects": [
        {
          "X": 22,
          "Y": -7,
          "Width": 10,
          "Height": 8,
          "Left": 22,
          "Right": 32,
          "Top": -7,
          "Bottom": 1,
          "Area": 80,
          "Center": {
            "X": 27,
            "Y": -3
          }
        },
        {
          "X": 24,
          "Y": 1,
          "Width": 6,
          "Height": 1,
          "Left": 24,
          "Right": 30,
          "Top": 1,
          "Bottom": 2,
          "Area": 6,
          "Center": {
            "X": 27,
            "Y": 1.5
          }
        },
        {
          "X": 24,
          "Y": -8,
          "Width": 6,
          "Height": 1,
          "Left": 24,
          "Right": 30,
          "Top": -8,
          "Bottom": -7,
          "Area": 6,
          "Center": {
            "X": 27,
            "Y": -7.5
          }
        }
      ],
      "X": 22,
      "Y": -8,
      "Width": 10,
      "Height": 10
    },
    {
      "Id": "compartment-7",
      "Name": "\u041F\u0435\u0440\u0435\u0445\u043E\u0434\u043D\u044B\u0439 \u043E\u0442\u0441\u0435\u043A \u0430\u0432\u0430\u043D\u043F\u043E\u0441\u0442\u0430",
      "Rects": [
        {
          "X": 34,
          "Y": 2,
          "Width": 6,
          "Height": 10,
          "Left": 34,
          "Right": 40,
          "Top": 2,
          "Bottom": 12,
          "Area": 60,
          "Center": {
            "X": 37,
            "Y": 7
          }
        },
        {
          "X": 40,
          "Y": 4,
          "Width": 2,
          "Height": 6,
          "Left": 40,
          "Right": 42,
          "Top": 4,
          "Bottom": 10,
          "Area": 12,
          "Center": {
            "X": 41,
            "Y": 7
          }
        },
        {
          "X": 32,
          "Y": 4,
          "Width": 2,
          "Height": 6,
          "Left": 32,
          "Right": 34,
          "Top": 4,
          "Bottom": 10,
          "Area": 12,
          "Center": {
            "X": 33,
            "Y": 7
          }
        }
      ],
      "X": 32,
      "Y": 2,
      "Width": 10,
      "Height": 10
    },
    {
      "Id": "compartment-10",
      "Name": "\u0421\u0442\u044B\u043A\u043E\u0432\u043E\u0447\u043D\u044B\u0439 \u0448\u043B\u044E\u0437 \u0430\u0432\u0430\u043D\u043F\u043E\u0441\u0442\u0430",
      "Rects": [
        {
          "X": 45,
          "Y": 1,
          "Width": 4,
          "Height": 12,
          "Left": 45,
          "Right": 49,
          "Top": 1,
          "Bottom": 13,
          "Area": 48,
          "Center": {
            "X": 47,
            "Y": 7
          }
        },
        {
          "X": 42,
          "Y": 4,
          "Width": 3,
          "Height": 6,
          "Left": 42,
          "Right": 45,
          "Top": 4,
          "Bottom": 10,
          "Area": 18,
          "Center": {
            "X": 43.5,
            "Y": 7
          }
        },
        {
          "X": 49,
          "Y": 4,
          "Width": 3,
          "Height": 6,
          "Left": 49,
          "Right": 52,
          "Top": 4,
          "Bottom": 10,
          "Area": 18,
          "Center": {
            "X": 50.5,
            "Y": 7
          }
        }
      ],
      "X": 42,
      "Y": 1,
      "Width": 10,
      "Height": 12
    }
  ],
  "Doors": [],
  "Airlocks": [],
  "Devices": [
    {
      "Kind": "Chair",
      "X": 14.5,
      "Y": 7.5,
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
      "Kind": "Chair",
      "X": 14.5,
      "Y": 6.5,
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
      "Kind": "Helm",
      "X": 18,
      "Y": 8,
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
      "Kind": "Navigation",
      "X": 18,
      "Y": 6,
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
      "Kind": "Navigation",
      "X": 16,
      "Y": 6,
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
      "X": 16,
      "Y": 8,
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
      "Kind": "Chair",
      "X": 19.5,
      "Y": 6.5,
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
      "Kind": "Chair",
      "X": 19.5,
      "Y": 7.5,
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
      "Kind": "Reactor",
      "X": 29,
      "Y": 17,
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
      "Kind": "Junction",
      "X": 23,
      "Y": 15.5,
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
      "X": 23,
      "Y": 16.5,
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
      "X": 23,
      "Y": 17.5,
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
      "X": 23,
      "Y": 18.5,
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
      "Kind": "Distribution",
      "X": 26.5,
      "Y": 17.5,
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
      "Kind": "Battery",
      "X": 26.5,
      "Y": 16.5,
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
      "Kind": "Junction",
      "X": 44.5,
      "Y": 5,
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
      "X": 25.5,
      "Y": -2,
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
      "X": 29,
      "Y": -4,
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
      "Kind": "Junction",
      "X": 43.5,
      "Y": 9,
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
      "Kind": "DecorativePlant",
      "X": 48.5,
      "Y": 8.5,
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
      "X": 25.5,
      "Y": -4,
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
      "Kind": "Junction",
      "X": 44.5,
      "Y": 9,
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
      "Kind": "DecorativePlant",
      "X": 45.5,
      "Y": 5.5,
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
      "Kind": "Junction",
      "X": 43.5,
      "Y": 5,
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
      "Kind": "DecorativePlant",
      "X": 48.5,
      "Y": 5.5,
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
      "Kind": "DecorativePlant",
      "X": 45.5,
      "Y": 8.5,
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
      "Kind": "ConstructionBench",
      "X": 28.5,
      "Y": -2,
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
      "Kind": "WallLamp",
      "X": 13.5,
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
      "X": 13.5,
      "Y": 10.5,
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
      "Kind": "Terminal",
      "X": 15.5,
      "Y": 11.5,
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
      "Kind": "Terminal",
      "X": 16.5,
      "Y": 11.5,
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
      "Kind": "Terminal",
      "X": 17.5,
      "Y": 11.5,
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
      "Kind": "Terminal",
      "X": 18.5,
      "Y": 11.5,
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
      "X": 20.5,
      "Y": 10.5,
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
      "X": 24.5,
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
      "Kind": "Terminal",
      "X": 24.5,
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
      "Kind": "Terminal",
      "X": 24.5,
      "Y": 8.5,
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
      "X": 24.5,
      "Y": 10.5,
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
      "X": 29.5,
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
      "Kind": "Terminal",
      "X": 29.5,
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
      "Kind": "Terminal",
      "X": 29.5,
      "Y": 8.5,
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
      "X": 29.5,
      "Y": 10.5,
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
      "Y": 4.5,
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
      "X": 23.5,
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
      "X": 30.5,
      "Y": 4.5,
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
      "X": 30.5,
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
      "X": 22.5,
      "Y": 14.5,
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
      "Y": 19.5,
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
      "Kind": "Terminal",
      "X": 26.5,
      "Y": 15.5,
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
      "X": 26.5,
      "Y": 18.5,
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
      "X": 31.5,
      "Y": 14.5,
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
      "X": 31.5,
      "Y": 19.5,
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
      "X": 28.5,
      "Y": 12.5,
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
      "X": 28.5,
      "Y": 21.5,
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
      "X": 30.5,
      "Y": 0.5,
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
      "X": 30.5,
      "Y": -6.5,
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
      "X": 23.5,
      "Y": 0.5,
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
      "X": 23.5,
      "Y": -6.5,
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
      "X": 33.5,
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
      "X": 33.5,
      "Y": 4.5,
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
      "X": 40.5,
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
      "X": 40.5,
      "Y": 4.5,
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
      "X": 39.5,
      "Y": 10.5,
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
      "X": 39.5,
      "Y": 8.5,
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
      "Kind": "Terminal",
      "X": 39.5,
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
      "X": 39.5,
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
      "X": 34.5,
      "Y": 10.5,
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
      "Kind": "Terminal",
      "X": 34.5,
      "Y": 8.5,
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
      "Kind": "Terminal",
      "X": 34.5,
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
      "X": 34.5,
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
      "X": 51.5,
      "Y": 8.5,
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
      "X": 51.5,
      "Y": 5.5,
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
      "X": 42.5,
      "Y": 8.5,
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
      "X": 42.5,
      "Y": 5.5,
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
    }
  ],
  "ForwardDegrees": -90,
  "WallMaterialsRaw": [],
  "EnginesRaw": [],
  "SupplementalWallTilesRaw": [
    {
      "X": 13,
      "Y": 4
    },
    {
      "X": 13,
      "Y": 9
    },
    {
      "X": 16,
      "Y": 5
    },
    {
      "X": 16,
      "Y": 6
    },
    {
      "X": 16,
      "Y": 7
    },
    {
      "X": 16,
      "Y": 8
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
      "Y": 7
    },
    {
      "X": 17,
      "Y": 8
    },
    {
      "X": 20,
      "Y": 4
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
      "X": 21,
      "Y": 8
    },
    {
      "X": 24,
      "Y": 4
    },
    {
      "X": 24,
      "Y": 9
    },
    {
      "X": 25,
      "Y": 11
    },
    {
      "X": 28,
      "Y": 11
    },
    {
      "X": 29,
      "Y": 4
    },
    {
      "X": 29,
      "Y": 9
    },
    {
      "X": 31,
      "Y": 5
    },
    {
      "X": 31,
      "Y": 8
    },
    {
      "X": 24,
      "Y": 13
    },
    {
      "X": 24,
      "Y": 20
    },
    {
      "X": 26,
      "Y": 15
    },
    {
      "X": 26,
      "Y": 18
    },
    {
      "X": 29,
      "Y": 13
    },
    {
      "X": 29,
      "Y": 20
    },
    {
      "X": 28,
      "Y": 1
    },
    {
      "X": 25,
      "Y": 1
    },
    {
      "X": 29,
      "Y": 0
    },
    {
      "X": 29,
      "Y": -7
    },
    {
      "X": 26,
      "Y": -3
    },
    {
      "X": 26,
      "Y": -4
    },
    {
      "X": 25,
      "Y": -3
    },
    {
      "X": 25,
      "Y": -4
    },
    {
      "X": 24,
      "Y": 0
    },
    {
      "X": 24,
      "Y": -3
    },
    {
      "X": 24,
      "Y": -4
    },
    {
      "X": 24,
      "Y": -7
    },
    {
      "X": 41,
      "Y": 8
    },
    {
      "X": 41,
      "Y": 5
    },
    {
      "X": 39,
      "Y": 9
    },
    {
      "X": 39,
      "Y": 4
    },
    {
      "X": 34,
      "Y": 9
    },
    {
      "X": 34,
      "Y": 4
    },
    {
      "X": 48,
      "Y": 9
    },
    {
      "X": 48,
      "Y": 4
    },
    {
      "X": 45,
      "Y": 9
    },
    {
      "X": 45,
      "Y": 4
    }
  ],
  "ForcedFloorTilesRaw": [
    {
      "X": 27,
      "Y": 12
    },
    {
      "X": 27,
      "Y": 13
    },
    {
      "X": 26,
      "Y": 12
    },
    {
      "X": 27,
      "Y": 14
    },
    {
      "X": 28,
      "Y": 13
    },
    {
      "X": 26,
      "Y": 13
    },
    {
      "X": 27,
      "Y": 15
    },
    {
      "X": 28,
      "Y": 14
    },
    {
      "X": 26,
      "Y": 14
    },
    {
      "X": 25,
      "Y": 13
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
      "X": 29,
      "Y": 14
    },
    {
      "X": 25,
      "Y": 14
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
      "X": 26,
      "Y": 16
    },
    {
      "X": 29,
      "Y": 15
    },
    {
      "X": 30,
      "Y": 14
    },
    {
      "X": 25,
      "Y": 15
    },
    {
      "X": 24,
      "Y": 14
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
      "X": 26,
      "Y": 17
    },
    {
      "X": 29,
      "Y": 16
    },
    {
      "X": 25,
      "Y": 16
    },
    {
      "X": 30,
      "Y": 15
    },
    {
      "X": 24,
      "Y": 15
    },
    {
      "X": 23,
      "Y": 14
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
      "X": 29,
      "Y": 17
    },
    {
      "X": 25,
      "Y": 17
    },
    {
      "X": 30,
      "Y": 16
    },
    {
      "X": 24,
      "Y": 16
    },
    {
      "X": 23,
      "Y": 15
    },
    {
      "X": 27,
      "Y": 20
    },
    {
      "X": 28,
      "Y": 19
    },
    {
      "X": 26,
      "Y": 19
    },
    {
      "X": 29,
      "Y": 18
    },
    {
      "X": 30,
      "Y": 17
    },
    {
      "X": 25,
      "Y": 18
    },
    {
      "X": 24,
      "Y": 17
    },
    {
      "X": 23,
      "Y": 16
    },
    {
      "X": 27,
      "Y": 21
    },
    {
      "X": 28,
      "Y": 20
    },
    {
      "X": 26,
      "Y": 20
    },
    {
      "X": 29,
      "Y": 19
    },
    {
      "X": 25,
      "Y": 19
    },
    {
      "X": 30,
      "Y": 18
    },
    {
      "X": 24,
      "Y": 18
    },
    {
      "X": 23,
      "Y": 17
    },
    {
      "X": 26,
      "Y": 21
    },
    {
      "X": 25,
      "Y": 20
    },
    {
      "X": 30,
      "Y": 19
    },
    {
      "X": 24,
      "Y": 19
    },
    {
      "X": 23,
      "Y": 18
    },
    {
      "X": 23,
      "Y": 19
    },
    {
      "X": 47,
      "Y": 12
    },
    {
      "X": 47,
      "Y": 11
    },
    {
      "X": 46,
      "Y": 12
    },
    {
      "X": 47,
      "Y": 10
    },
    {
      "X": 46,
      "Y": 11
    },
    {
      "X": 46,
      "Y": 10
    },
    {
      "X": 21,
      "Y": 7
    },
    {
      "X": 21,
      "Y": 6
    },
    {
      "X": 20,
      "Y": 7
    },
    {
      "X": 20,
      "Y": 6
    },
    {
      "X": 20,
      "Y": 8
    },
    {
      "X": 19,
      "Y": 7
    },
    {
      "X": 20,
      "Y": 5
    },
    {
      "X": 19,
      "Y": 6
    },
    {
      "X": 19,
      "Y": 8
    },
    {
      "X": 18,
      "Y": 7
    },
    {
      "X": 19,
      "Y": 5
    },
    {
      "X": 18,
      "Y": 6
    },
    {
      "X": 19,
      "Y": 9
    },
    {
      "X": 18,
      "Y": 8
    },
    {
      "X": 19,
      "Y": 4
    },
    {
      "X": 18,
      "Y": 5
    },
    {
      "X": 19,
      "Y": 10
    },
    {
      "X": 18,
      "Y": 9
    },
    {
      "X": 19,
      "Y": 3
    },
    {
      "X": 18,
      "Y": 4
    },
    {
      "X": 18,
      "Y": 10
    },
    {
      "X": 17,
      "Y": 9
    },
    {
      "X": 18,
      "Y": 3
    },
    {
      "X": 17,
      "Y": 4
    },
    {
      "X": 17,
      "Y": 10
    },
    {
      "X": 16,
      "Y": 9
    },
    {
      "X": 17,
      "Y": 3
    },
    {
      "X": 16,
      "Y": 4
    },
    {
      "X": 16,
      "Y": 10
    },
    {
      "X": 15,
      "Y": 9
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
      "X": 15,
      "Y": 10
    },
    {
      "X": 15,
      "Y": 8
    },
    {
      "X": 14,
      "Y": 9
    },
    {
      "X": 15,
      "Y": 3
    },
    {
      "X": 15,
      "Y": 5
    },
    {
      "X": 14,
      "Y": 4
    },
    {
      "X": 14,
      "Y": 10
    },
    {
      "X": 15,
      "Y": 7
    },
    {
      "X": 14,
      "Y": 8
    },
    {
      "X": 14,
      "Y": 3
    },
    {
      "X": 15,
      "Y": 6
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
      "X": 13,
      "Y": 8
    },
    {
      "X": 14,
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
      "X": 13,
      "Y": 6
    },
    {
      "X": 12,
      "Y": 7
    },
    {
      "X": 12,
      "Y": 6
    },
    {
      "X": 31,
      "Y": 7
    },
    {
      "X": 31,
      "Y": 6
    },
    {
      "X": 30,
      "Y": 7
    },
    {
      "X": 30,
      "Y": 6
    },
    {
      "X": 30,
      "Y": 8
    },
    {
      "X": 29,
      "Y": 7
    },
    {
      "X": 30,
      "Y": 5
    },
    {
      "X": 29,
      "Y": 6
    },
    {
      "X": 29,
      "Y": 8
    },
    {
      "X": 28,
      "Y": 7
    },
    {
      "X": 29,
      "Y": 5
    },
    {
      "X": 28,
      "Y": 6
    },
    {
      "X": 28,
      "Y": 8
    },
    {
      "X": 27,
      "Y": 7
    },
    {
      "X": 28,
      "Y": 5
    },
    {
      "X": 27,
      "Y": 6
    },
    {
      "X": 28,
      "Y": 9
    },
    {
      "X": 27,
      "Y": 8
    },
    {
      "X": 26,
      "Y": 7
    },
    {
      "X": 28,
      "Y": 4
    },
    {
      "X": 27,
      "Y": 5
    },
    {
      "X": 26,
      "Y": 6
    },
    {
      "X": 28,
      "Y": 10
    },
    {
      "X": 27,
      "Y": 9
    },
    {
      "X": 26,
      "Y": 8
    },
    {
      "X": 25,
      "Y": 7
    },
    {
      "X": 28,
      "Y": 3
    },
    {
      "X": 27,
      "Y": 4
    },
    {
      "X": 26,
      "Y": 5
    },
    {
      "X": 25,
      "Y": 6
    },
    {
      "X": 27,
      "Y": 10
    },
    {
      "X": 26,
      "Y": 9
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
      "X": 27,
      "Y": 3
    },
    {
      "X": 26,
      "Y": 4
    },
    {
      "X": 25,
      "Y": 5
    },
    {
      "X": 24,
      "Y": 6
    },
    {
      "X": 27,
      "Y": 11
    },
    {
      "X": 26,
      "Y": 10
    },
    {
      "X": 25,
      "Y": 9
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
      "X": 27,
      "Y": 2
    },
    {
      "X": 26,
      "Y": 3
    },
    {
      "X": 25,
      "Y": 4
    },
    {
      "X": 24,
      "Y": 5
    },
    {
      "X": 23,
      "Y": 6
    },
    {
      "X": 26,
      "Y": 11
    },
    {
      "X": 25,
      "Y": 10
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
      "X": 26,
      "Y": 2
    },
    {
      "X": 25,
      "Y": 3
    },
    {
      "X": 23,
      "Y": 5
    },
    {
      "X": 22,
      "Y": 6
    },
    {
      "X": 41,
      "Y": 7
    },
    {
      "X": 41,
      "Y": 6
    },
    {
      "X": 40,
      "Y": 7
    },
    {
      "X": 40,
      "Y": 6
    },
    {
      "X": 40,
      "Y": 8
    },
    {
      "X": 39,
      "Y": 7
    },
    {
      "X": 40,
      "Y": 5
    },
    {
      "X": 39,
      "Y": 6
    },
    {
      "X": 39,
      "Y": 8
    },
    {
      "X": 38,
      "Y": 7
    },
    {
      "X": 39,
      "Y": 5
    },
    {
      "X": 38,
      "Y": 6
    },
    {
      "X": 38,
      "Y": 8
    },
    {
      "X": 37,
      "Y": 7
    },
    {
      "X": 38,
      "Y": 5
    },
    {
      "X": 37,
      "Y": 6
    },
    {
      "X": 38,
      "Y": 9
    },
    {
      "X": 37,
      "Y": 8
    },
    {
      "X": 36,
      "Y": 7
    },
    {
      "X": 38,
      "Y": 4
    },
    {
      "X": 37,
      "Y": 5
    },
    {
      "X": 36,
      "Y": 6
    },
    {
      "X": 38,
      "Y": 10
    },
    {
      "X": 37,
      "Y": 9
    },
    {
      "X": 36,
      "Y": 8
    },
    {
      "X": 35,
      "Y": 7
    },
    {
      "X": 38,
      "Y": 3
    },
    {
      "X": 37,
      "Y": 4
    },
    {
      "X": 36,
      "Y": 5
    },
    {
      "X": 35,
      "Y": 6
    },
    {
      "X": 37,
      "Y": 10
    },
    {
      "X": 36,
      "Y": 9
    },
    {
      "X": 35,
      "Y": 8
    },
    {
      "X": 34,
      "Y": 7
    },
    {
      "X": 37,
      "Y": 3
    },
    {
      "X": 36,
      "Y": 4
    },
    {
      "X": 35,
      "Y": 5
    },
    {
      "X": 34,
      "Y": 6
    },
    {
      "X": 37,
      "Y": 11
    },
    {
      "X": 36,
      "Y": 10
    },
    {
      "X": 35,
      "Y": 9
    },
    {
      "X": 34,
      "Y": 8
    },
    {
      "X": 33,
      "Y": 7
    },
    {
      "X": 37,
      "Y": 2
    },
    {
      "X": 36,
      "Y": 3
    },
    {
      "X": 35,
      "Y": 4
    },
    {
      "X": 34,
      "Y": 5
    },
    {
      "X": 33,
      "Y": 6
    },
    {
      "X": 36,
      "Y": 11
    },
    {
      "X": 35,
      "Y": 10
    },
    {
      "X": 33,
      "Y": 8
    },
    {
      "X": 32,
      "Y": 7
    },
    {
      "X": 36,
      "Y": 2
    },
    {
      "X": 35,
      "Y": 3
    },
    {
      "X": 33,
      "Y": 5
    },
    {
      "X": 32,
      "Y": 6
    },
    {
      "X": 27,
      "Y": 1
    },
    {
      "X": 27,
      "Y": 0
    },
    {
      "X": 26,
      "Y": 1
    },
    {
      "X": 27,
      "Y": -1
    },
    {
      "X": 28,
      "Y": 0
    },
    {
      "X": 26,
      "Y": 0
    },
    {
      "X": 27,
      "Y": -2
    },
    {
      "X": 28,
      "Y": -1
    },
    {
      "X": 26,
      "Y": -1
    },
    {
      "X": 25,
      "Y": 0
    },
    {
      "X": 27,
      "Y": -3
    },
    {
      "X": 28,
      "Y": -2
    },
    {
      "X": 26,
      "Y": -2
    },
    {
      "X": 29,
      "Y": -1
    },
    {
      "X": 25,
      "Y": -1
    },
    {
      "X": 27,
      "Y": -4
    },
    {
      "X": 28,
      "Y": -3
    },
    {
      "X": 25,
      "Y": -2
    },
    {
      "X": 30,
      "Y": -1
    },
    {
      "X": 24,
      "Y": -1
    },
    {
      "X": 27,
      "Y": -5
    },
    {
      "X": 29,
      "Y": -2
    },
    {
      "X": 24,
      "Y": -2
    },
    {
      "X": 30,
      "Y": -2
    },
    {
      "X": 23,
      "Y": -1
    },
    {
      "X": 27,
      "Y": -6
    },
    {
      "X": 28,
      "Y": -5
    },
    {
      "X": 26,
      "Y": -5
    },
    {
      "X": 23,
      "Y": -2
    },
    {
      "X": 30,
      "Y": -3
    },
    {
      "X": 27,
      "Y": -7
    },
    {
      "X": 28,
      "Y": -6
    },
    {
      "X": 26,
      "Y": -6
    },
    {
      "X": 25,
      "Y": -5
    },
    {
      "X": 23,
      "Y": -3
    },
    {
      "X": 30,
      "Y": -4
    },
    {
      "X": 27,
      "Y": -8
    },
    {
      "X": 28,
      "Y": -7
    },
    {
      "X": 26,
      "Y": -7
    },
    {
      "X": 29,
      "Y": -6
    },
    {
      "X": 25,
      "Y": -6
    },
    {
      "X": 24,
      "Y": -5
    },
    {
      "X": 23,
      "Y": -4
    },
    {
      "X": 30,
      "Y": -5
    },
    {
      "X": 26,
      "Y": -8
    },
    {
      "X": 25,
      "Y": -7
    },
    {
      "X": 30,
      "Y": -6
    },
    {
      "X": 24,
      "Y": -6
    },
    {
      "X": 23,
      "Y": -5
    },
    {
      "X": 23,
      "Y": -6
    },
    {
      "X": 29,
      "Y": -3
    },
    {
      "X": 47,
      "Y": 3
    },
    {
      "X": 47,
      "Y": 2
    },
    {
      "X": 46,
      "Y": 3
    },
    {
      "X": 47,
      "Y": 1
    },
    {
      "X": 46,
      "Y": 2
    },
    {
      "X": 46,
      "Y": 1
    },
    {
      "X": 47,
      "Y": 9
    },
    {
      "X": 47,
      "Y": 8
    },
    {
      "X": 46,
      "Y": 9
    },
    {
      "X": 47,
      "Y": 7
    },
    {
      "X": 48,
      "Y": 8
    },
    {
      "X": 46,
      "Y": 8
    },
    {
      "X": 47,
      "Y": 6
    },
    {
      "X": 48,
      "Y": 7
    },
    {
      "X": 46,
      "Y": 7
    },
    {
      "X": 49,
      "Y": 8
    },
    {
      "X": 45,
      "Y": 8
    },
    {
      "X": 47,
      "Y": 5
    },
    {
      "X": 48,
      "Y": 6
    },
    {
      "X": 46,
      "Y": 6
    },
    {
      "X": 49,
      "Y": 7
    },
    {
      "X": 45,
      "Y": 7
    },
    {
      "X": 50,
      "Y": 8
    },
    {
      "X": 44,
      "Y": 8
    },
    {
      "X": 47,
      "Y": 4
    },
    {
      "X": 48,
      "Y": 5
    },
    {
      "X": 46,
      "Y": 5
    },
    {
      "X": 49,
      "Y": 6
    },
    {
      "X": 45,
      "Y": 6
    },
    {
      "X": 50,
      "Y": 7
    },
    {
      "X": 44,
      "Y": 7
    },
    {
      "X": 43,
      "Y": 8
    },
    {
      "X": 46,
      "Y": 4
    },
    {
      "X": 49,
      "Y": 5
    },
    {
      "X": 45,
      "Y": 5
    },
    {
      "X": 50,
      "Y": 6
    },
    {
      "X": 44,
      "Y": 6
    },
    {
      "X": 51,
      "Y": 7
    },
    {
      "X": 43,
      "Y": 7
    },
    {
      "X": 50,
      "Y": 5
    },
    {
      "X": 44,
      "Y": 5
    },
    {
      "X": 51,
      "Y": 6
    },
    {
      "X": 43,
      "Y": 6
    },
    {
      "X": 42,
      "Y": 7
    },
    {
      "X": 43,
      "Y": 5
    },
    {
      "X": 42,
      "Y": 6
    }
  ],
  "WallOpenSidesRaw": [
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
      "Y": 9,
      "Side": "West"
    },
    {
      "X": 13,
      "Y": 10,
      "Side": "West"
    },
    {
      "X": 14,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 14,
      "Y": 11,
      "Side": "South"
    },
    {
      "X": 15,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 15,
      "Y": 11,
      "Side": "South"
    },
    {
      "X": 16,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 16,
      "Y": 5,
      "Side": "East"
    },
    {
      "X": 16,
      "Y": 6,
      "Side": "East"
    },
    {
      "X": 16,
      "Y": 7,
      "Side": "East"
    },
    {
      "X": 16,
      "Y": 8,
      "Side": "East"
    },
    {
      "X": 16,
      "Y": 11,
      "Side": "South"
    },
    {
      "X": 17,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 17,
      "Y": 5,
      "Side": "West"
    },
    {
      "X": 17,
      "Y": 6,
      "Side": "West"
    },
    {
      "X": 17,
      "Y": 7,
      "Side": "West"
    },
    {
      "X": 17,
      "Y": 8,
      "Side": "West"
    },
    {
      "X": 17,
      "Y": 11,
      "Side": "South"
    },
    {
      "X": 18,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 18,
      "Y": 11,
      "Side": "South"
    },
    {
      "X": 19,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 19,
      "Y": 11,
      "Side": "South"
    },
    {
      "X": 20,
      "Y": 3,
      "Side": "East"
    },
    {
      "X": 20,
      "Y": 4,
      "Side": "East"
    },
    {
      "X": 20,
      "Y": 9,
      "Side": "East"
    },
    {
      "X": 20,
      "Y": 10,
      "Side": "East"
    },
    {
      "X": 12,
      "Y": 5,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": 8,
      "Side": "West"
    },
    {
      "X": 21,
      "Y": 5,
      "Side": "East"
    },
    {
      "X": 21,
      "Y": 8,
      "Side": "East"
    },
    {
      "X": 24,
      "Y": 3,
      "Side": "West"
    },
    {
      "X": 24,
      "Y": 10,
      "Side": "West"
    },
    {
      "X": 25,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 25,
      "Y": 11,
      "Side": "South"
    },
    {
      "X": 28,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 28,
      "Y": 11,
      "Side": "South"
    },
    {
      "X": 29,
      "Y": 3,
      "Side": "East"
    },
    {
      "X": 29,
      "Y": 10,
      "Side": "East"
    },
    {
      "X": 22,
      "Y": 5,
      "Side": "West"
    },
    {
      "X": 22,
      "Y": 8,
      "Side": "West"
    },
    {
      "X": 23,
      "Y": 4,
      "Side": "North"
    },
    {
      "X": 23,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 30,
      "Y": 4,
      "Side": "North"
    },
    {
      "X": 30,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 31,
      "Y": 5,
      "Side": "East"
    },
    {
      "X": 31,
      "Y": 8,
      "Side": "East"
    },
    {
      "X": 22,
      "Y": 14,
      "Side": "West"
    },
    {
      "X": 22,
      "Y": 15,
      "Side": "West"
    },
    {
      "X": 22,
      "Y": 16,
      "Side": "West"
    },
    {
      "X": 22,
      "Y": 17,
      "Side": "West"
    },
    {
      "X": 22,
      "Y": 18,
      "Side": "West"
    },
    {
      "X": 22,
      "Y": 19,
      "Side": "West"
    },
    {
      "X": 23,
      "Y": 13,
      "Side": "North"
    },
    {
      "X": 23,
      "Y": 20,
      "Side": "South"
    },
    {
      "X": 26,
      "Y": 15,
      "Side": "East"
    },
    {
      "X": 26,
      "Y": 18,
      "Side": "East"
    },
    {
      "X": 30,
      "Y": 13,
      "Side": "North"
    },
    {
      "X": 30,
      "Y": 20,
      "Side": "South"
    },
    {
      "X": 31,
      "Y": 14,
      "Side": "East"
    },
    {
      "X": 31,
      "Y": 19,
      "Side": "East"
    },
    {
      "X": 25,
      "Y": 12,
      "Side": "North"
    },
    {
      "X": 28,
      "Y": 12,
      "Side": "North"
    },
    {
      "X": 25,
      "Y": 21,
      "Side": "South"
    },
    {
      "X": 28,
      "Y": 21,
      "Side": "South"
    },
    {
      "X": 22,
      "Y": -6,
      "Side": "West"
    },
    {
      "X": 22,
      "Y": -5,
      "Side": "West"
    },
    {
      "X": 22,
      "Y": -4,
      "Side": "West"
    },
    {
      "X": 22,
      "Y": -3,
      "Side": "West"
    },
    {
      "X": 22,
      "Y": -2,
      "Side": "West"
    },
    {
      "X": 22,
      "Y": -1,
      "Side": "West"
    },
    {
      "X": 28,
      "Y": -8,
      "Side": "North"
    },
    {
      "X": 25,
      "Y": -8,
      "Side": "North"
    },
    {
      "X": 28,
      "Y": 1,
      "Side": "South"
    },
    {
      "X": 25,
      "Y": 1,
      "Side": "South"
    },
    {
      "X": 31,
      "Y": -1,
      "Side": "East"
    },
    {
      "X": 31,
      "Y": -2,
      "Side": "East"
    },
    {
      "X": 31,
      "Y": -3,
      "Side": "East"
    },
    {
      "X": 31,
      "Y": -4,
      "Side": "East"
    },
    {
      "X": 31,
      "Y": -5,
      "Side": "East"
    },
    {
      "X": 31,
      "Y": -6,
      "Side": "East"
    },
    {
      "X": 30,
      "Y": 0,
      "Side": "South"
    },
    {
      "X": 30,
      "Y": -7,
      "Side": "North"
    },
    {
      "X": 29,
      "Y": 0,
      "Side": "South"
    },
    {
      "X": 29,
      "Y": -7,
      "Side": "North"
    },
    {
      "X": 26,
      "Y": -3,
      "Side": "North"
    },
    {
      "X": 26,
      "Y": -4,
      "Side": "South"
    },
    {
      "X": 25,
      "Y": -3,
      "Side": "North"
    },
    {
      "X": 25,
      "Y": -4,
      "Side": "South"
    },
    {
      "X": 24,
      "Y": 0,
      "Side": "South"
    },
    {
      "X": 24,
      "Y": -3,
      "Side": "North"
    },
    {
      "X": 24,
      "Y": -4,
      "Side": "South"
    },
    {
      "X": 24,
      "Y": -7,
      "Side": "North"
    },
    {
      "X": 23,
      "Y": 0,
      "Side": "South"
    },
    {
      "X": 23,
      "Y": -7,
      "Side": "North"
    },
    {
      "X": 33,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 33,
      "Y": 4,
      "Side": "North"
    },
    {
      "X": 32,
      "Y": 8,
      "Side": "West"
    },
    {
      "X": 32,
      "Y": 5,
      "Side": "West"
    },
    {
      "X": 41,
      "Y": 8,
      "Side": "East"
    },
    {
      "X": 41,
      "Y": 5,
      "Side": "East"
    },
    {
      "X": 40,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 40,
      "Y": 4,
      "Side": "North"
    },
    {
      "X": 39,
      "Y": 10,
      "Side": "East"
    },
    {
      "X": 39,
      "Y": 3,
      "Side": "East"
    },
    {
      "X": 38,
      "Y": 11,
      "Side": "South"
    },
    {
      "X": 38,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 35,
      "Y": 11,
      "Side": "South"
    },
    {
      "X": 35,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 34,
      "Y": 10,
      "Side": "West"
    },
    {
      "X": 34,
      "Y": 3,
      "Side": "West"
    },
    {
      "X": 51,
      "Y": 8,
      "Side": "East"
    },
    {
      "X": 51,
      "Y": 5,
      "Side": "East"
    },
    {
      "X": 50,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 50,
      "Y": 4,
      "Side": "North"
    },
    {
      "X": 49,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 49,
      "Y": 4,
      "Side": "North"
    },
    {
      "X": 44,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 44,
      "Y": 4,
      "Side": "North"
    },
    {
      "X": 43,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 43,
      "Y": 4,
      "Side": "North"
    },
    {
      "X": 42,
      "Y": 8,
      "Side": "West"
    },
    {
      "X": 42,
      "Y": 5,
      "Side": "West"
    },
    {
      "X": 48,
      "Y": 11,
      "Side": "East"
    },
    {
      "X": 48,
      "Y": 10,
      "Side": "East"
    },
    {
      "X": 48,
      "Y": 3,
      "Side": "East"
    },
    {
      "X": 48,
      "Y": 2,
      "Side": "East"
    },
    {
      "X": 45,
      "Y": 11,
      "Side": "West"
    },
    {
      "X": 45,
      "Y": 10,
      "Side": "West"
    },
    {
      "X": 45,
      "Y": 3,
      "Side": "West"
    },
    {
      "X": 45,
      "Y": 2,
      "Side": "West"
    }
  ],
  "DoorEdgesRaw": [
    {
      "X": 21,
      "Y": 6,
      "Side": "East",
      "Id": "compartment-0-door-g28"
    },
    {
      "X": 21,
      "Y": 7,
      "Side": "East",
      "Id": "compartment-0-door-g28"
    },
    {
      "X": 11,
      "Y": 6,
      "Side": "East",
      "Id": "compartment-0-door-g29"
    },
    {
      "X": 11,
      "Y": 7,
      "Side": "East",
      "Id": "compartment-0-door-g29"
    },
    {
      "X": 36,
      "Y": 1,
      "Side": "South",
      "Id": "compartment-7-door-g24"
    },
    {
      "X": 37,
      "Y": 1,
      "Side": "South",
      "Id": "compartment-7-door-g24"
    },
    {
      "X": 26,
      "Y": 11,
      "Side": "South",
      "Id": "compartment-1-door-g24"
    },
    {
      "X": 27,
      "Y": 11,
      "Side": "South",
      "Id": "compartment-1-door-g24"
    },
    {
      "X": 26,
      "Y": -9,
      "Side": "South",
      "Id": "compartment-9-door-g28"
    },
    {
      "X": 27,
      "Y": -9,
      "Side": "South",
      "Id": "compartment-9-door-g28"
    },
    {
      "X": 27,
      "Y": 21,
      "Side": "South",
      "Id": "compartment-2-door-g26"
    },
    {
      "X": 26,
      "Y": 21,
      "Side": "South",
      "Id": "compartment-2-door-g26"
    },
    {
      "X": 37,
      "Y": 11,
      "Side": "South",
      "Id": "compartment-7-door-g25"
    },
    {
      "X": 36,
      "Y": 11,
      "Side": "South",
      "Id": "compartment-7-door-g25"
    },
    {
      "X": 31,
      "Y": 7,
      "Side": "East",
      "Id": "compartment-7-door-g23"
    },
    {
      "X": 31,
      "Y": 6,
      "Side": "East",
      "Id": "compartment-7-door-g23"
    },
    {
      "X": 41,
      "Y": 7,
      "Side": "East",
      "Id": "compartment-7-door-g22"
    },
    {
      "X": 41,
      "Y": 6,
      "Side": "East",
      "Id": "compartment-7-door-g22"
    },
    {
      "X": 47,
      "Y": 0,
      "Side": "South",
      "Id": "compartment-10-door-g34"
    },
    {
      "X": 46,
      "Y": 0,
      "Side": "South",
      "Id": "compartment-10-door-g34"
    },
    {
      "X": 26,
      "Y": 1,
      "Side": "South",
      "Id": "compartment-9-door-g29"
    },
    {
      "X": 51,
      "Y": 7,
      "Side": "East",
      "Id": "compartment-10-door-g28"
    },
    {
      "X": 27,
      "Y": 1,
      "Side": "South",
      "Id": "compartment-9-door-g29"
    },
    {
      "X": 51,
      "Y": 6,
      "Side": "East",
      "Id": "compartment-10-door-g28"
    },
    {
      "X": 47,
      "Y": 3,
      "Side": "South",
      "Id": "compartment-10-door-g33"
    },
    {
      "X": 46,
      "Y": 3,
      "Side": "South",
      "Id": "compartment-10-door-g33"
    },
    {
      "X": 47,
      "Y": 12,
      "Side": "South",
      "Id": "compartment-10-door-g32"
    },
    {
      "X": 46,
      "Y": 12,
      "Side": "South",
      "Id": "compartment-10-door-g32"
    },
    {
      "X": 47,
      "Y": 9,
      "Side": "South",
      "Id": "compartment-10-door-g31"
    },
    {
      "X": 46,
      "Y": 9,
      "Side": "South",
      "Id": "compartment-10-door-g31"
    }
  ],
  "WreckPatchesRaw": null,
  "JunctionWallsRaw": null,
  "WiresRaw": null,
  "Wires": [],
  "WallMaterials": [],
  "Engines": [],
  "SupplementalWallTiles": [
    {
      "X": 13,
      "Y": 4
    },
    {
      "X": 13,
      "Y": 9
    },
    {
      "X": 16,
      "Y": 5
    },
    {
      "X": 16,
      "Y": 6
    },
    {
      "X": 16,
      "Y": 7
    },
    {
      "X": 16,
      "Y": 8
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
      "Y": 7
    },
    {
      "X": 17,
      "Y": 8
    },
    {
      "X": 20,
      "Y": 4
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
      "X": 21,
      "Y": 8
    },
    {
      "X": 24,
      "Y": 4
    },
    {
      "X": 24,
      "Y": 9
    },
    {
      "X": 25,
      "Y": 11
    },
    {
      "X": 28,
      "Y": 11
    },
    {
      "X": 29,
      "Y": 4
    },
    {
      "X": 29,
      "Y": 9
    },
    {
      "X": 31,
      "Y": 5
    },
    {
      "X": 31,
      "Y": 8
    },
    {
      "X": 24,
      "Y": 13
    },
    {
      "X": 24,
      "Y": 20
    },
    {
      "X": 26,
      "Y": 15
    },
    {
      "X": 26,
      "Y": 18
    },
    {
      "X": 29,
      "Y": 13
    },
    {
      "X": 29,
      "Y": 20
    },
    {
      "X": 28,
      "Y": 1
    },
    {
      "X": 25,
      "Y": 1
    },
    {
      "X": 29,
      "Y": 0
    },
    {
      "X": 29,
      "Y": -7
    },
    {
      "X": 26,
      "Y": -3
    },
    {
      "X": 26,
      "Y": -4
    },
    {
      "X": 25,
      "Y": -3
    },
    {
      "X": 25,
      "Y": -4
    },
    {
      "X": 24,
      "Y": 0
    },
    {
      "X": 24,
      "Y": -3
    },
    {
      "X": 24,
      "Y": -4
    },
    {
      "X": 24,
      "Y": -7
    },
    {
      "X": 41,
      "Y": 8
    },
    {
      "X": 41,
      "Y": 5
    },
    {
      "X": 39,
      "Y": 9
    },
    {
      "X": 39,
      "Y": 4
    },
    {
      "X": 34,
      "Y": 9
    },
    {
      "X": 34,
      "Y": 4
    },
    {
      "X": 48,
      "Y": 9
    },
    {
      "X": 48,
      "Y": 4
    },
    {
      "X": 45,
      "Y": 9
    },
    {
      "X": 45,
      "Y": 4
    }
  ],
  "ForcedFloorTiles": [
    {
      "X": 27,
      "Y": 12
    },
    {
      "X": 27,
      "Y": 13
    },
    {
      "X": 26,
      "Y": 12
    },
    {
      "X": 27,
      "Y": 14
    },
    {
      "X": 28,
      "Y": 13
    },
    {
      "X": 26,
      "Y": 13
    },
    {
      "X": 27,
      "Y": 15
    },
    {
      "X": 28,
      "Y": 14
    },
    {
      "X": 26,
      "Y": 14
    },
    {
      "X": 25,
      "Y": 13
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
      "X": 29,
      "Y": 14
    },
    {
      "X": 25,
      "Y": 14
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
      "X": 26,
      "Y": 16
    },
    {
      "X": 29,
      "Y": 15
    },
    {
      "X": 30,
      "Y": 14
    },
    {
      "X": 25,
      "Y": 15
    },
    {
      "X": 24,
      "Y": 14
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
      "X": 26,
      "Y": 17
    },
    {
      "X": 29,
      "Y": 16
    },
    {
      "X": 25,
      "Y": 16
    },
    {
      "X": 30,
      "Y": 15
    },
    {
      "X": 24,
      "Y": 15
    },
    {
      "X": 23,
      "Y": 14
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
      "X": 29,
      "Y": 17
    },
    {
      "X": 25,
      "Y": 17
    },
    {
      "X": 30,
      "Y": 16
    },
    {
      "X": 24,
      "Y": 16
    },
    {
      "X": 23,
      "Y": 15
    },
    {
      "X": 27,
      "Y": 20
    },
    {
      "X": 28,
      "Y": 19
    },
    {
      "X": 26,
      "Y": 19
    },
    {
      "X": 29,
      "Y": 18
    },
    {
      "X": 30,
      "Y": 17
    },
    {
      "X": 25,
      "Y": 18
    },
    {
      "X": 24,
      "Y": 17
    },
    {
      "X": 23,
      "Y": 16
    },
    {
      "X": 27,
      "Y": 21
    },
    {
      "X": 28,
      "Y": 20
    },
    {
      "X": 26,
      "Y": 20
    },
    {
      "X": 29,
      "Y": 19
    },
    {
      "X": 25,
      "Y": 19
    },
    {
      "X": 30,
      "Y": 18
    },
    {
      "X": 24,
      "Y": 18
    },
    {
      "X": 23,
      "Y": 17
    },
    {
      "X": 26,
      "Y": 21
    },
    {
      "X": 25,
      "Y": 20
    },
    {
      "X": 30,
      "Y": 19
    },
    {
      "X": 24,
      "Y": 19
    },
    {
      "X": 23,
      "Y": 18
    },
    {
      "X": 23,
      "Y": 19
    },
    {
      "X": 47,
      "Y": 12
    },
    {
      "X": 47,
      "Y": 11
    },
    {
      "X": 46,
      "Y": 12
    },
    {
      "X": 47,
      "Y": 10
    },
    {
      "X": 46,
      "Y": 11
    },
    {
      "X": 46,
      "Y": 10
    },
    {
      "X": 21,
      "Y": 7
    },
    {
      "X": 21,
      "Y": 6
    },
    {
      "X": 20,
      "Y": 7
    },
    {
      "X": 20,
      "Y": 6
    },
    {
      "X": 20,
      "Y": 8
    },
    {
      "X": 19,
      "Y": 7
    },
    {
      "X": 20,
      "Y": 5
    },
    {
      "X": 19,
      "Y": 6
    },
    {
      "X": 19,
      "Y": 8
    },
    {
      "X": 18,
      "Y": 7
    },
    {
      "X": 19,
      "Y": 5
    },
    {
      "X": 18,
      "Y": 6
    },
    {
      "X": 19,
      "Y": 9
    },
    {
      "X": 18,
      "Y": 8
    },
    {
      "X": 19,
      "Y": 4
    },
    {
      "X": 18,
      "Y": 5
    },
    {
      "X": 19,
      "Y": 10
    },
    {
      "X": 18,
      "Y": 9
    },
    {
      "X": 19,
      "Y": 3
    },
    {
      "X": 18,
      "Y": 4
    },
    {
      "X": 18,
      "Y": 10
    },
    {
      "X": 17,
      "Y": 9
    },
    {
      "X": 18,
      "Y": 3
    },
    {
      "X": 17,
      "Y": 4
    },
    {
      "X": 17,
      "Y": 10
    },
    {
      "X": 16,
      "Y": 9
    },
    {
      "X": 17,
      "Y": 3
    },
    {
      "X": 16,
      "Y": 4
    },
    {
      "X": 16,
      "Y": 10
    },
    {
      "X": 15,
      "Y": 9
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
      "X": 15,
      "Y": 10
    },
    {
      "X": 15,
      "Y": 8
    },
    {
      "X": 14,
      "Y": 9
    },
    {
      "X": 15,
      "Y": 3
    },
    {
      "X": 15,
      "Y": 5
    },
    {
      "X": 14,
      "Y": 4
    },
    {
      "X": 14,
      "Y": 10
    },
    {
      "X": 15,
      "Y": 7
    },
    {
      "X": 14,
      "Y": 8
    },
    {
      "X": 14,
      "Y": 3
    },
    {
      "X": 15,
      "Y": 6
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
      "X": 13,
      "Y": 8
    },
    {
      "X": 14,
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
      "X": 13,
      "Y": 6
    },
    {
      "X": 12,
      "Y": 7
    },
    {
      "X": 12,
      "Y": 6
    },
    {
      "X": 31,
      "Y": 7
    },
    {
      "X": 31,
      "Y": 6
    },
    {
      "X": 30,
      "Y": 7
    },
    {
      "X": 30,
      "Y": 6
    },
    {
      "X": 30,
      "Y": 8
    },
    {
      "X": 29,
      "Y": 7
    },
    {
      "X": 30,
      "Y": 5
    },
    {
      "X": 29,
      "Y": 6
    },
    {
      "X": 29,
      "Y": 8
    },
    {
      "X": 28,
      "Y": 7
    },
    {
      "X": 29,
      "Y": 5
    },
    {
      "X": 28,
      "Y": 6
    },
    {
      "X": 28,
      "Y": 8
    },
    {
      "X": 27,
      "Y": 7
    },
    {
      "X": 28,
      "Y": 5
    },
    {
      "X": 27,
      "Y": 6
    },
    {
      "X": 28,
      "Y": 9
    },
    {
      "X": 27,
      "Y": 8
    },
    {
      "X": 26,
      "Y": 7
    },
    {
      "X": 28,
      "Y": 4
    },
    {
      "X": 27,
      "Y": 5
    },
    {
      "X": 26,
      "Y": 6
    },
    {
      "X": 28,
      "Y": 10
    },
    {
      "X": 27,
      "Y": 9
    },
    {
      "X": 26,
      "Y": 8
    },
    {
      "X": 25,
      "Y": 7
    },
    {
      "X": 28,
      "Y": 3
    },
    {
      "X": 27,
      "Y": 4
    },
    {
      "X": 26,
      "Y": 5
    },
    {
      "X": 25,
      "Y": 6
    },
    {
      "X": 27,
      "Y": 10
    },
    {
      "X": 26,
      "Y": 9
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
      "X": 27,
      "Y": 3
    },
    {
      "X": 26,
      "Y": 4
    },
    {
      "X": 25,
      "Y": 5
    },
    {
      "X": 24,
      "Y": 6
    },
    {
      "X": 27,
      "Y": 11
    },
    {
      "X": 26,
      "Y": 10
    },
    {
      "X": 25,
      "Y": 9
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
      "X": 27,
      "Y": 2
    },
    {
      "X": 26,
      "Y": 3
    },
    {
      "X": 25,
      "Y": 4
    },
    {
      "X": 24,
      "Y": 5
    },
    {
      "X": 23,
      "Y": 6
    },
    {
      "X": 26,
      "Y": 11
    },
    {
      "X": 25,
      "Y": 10
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
      "X": 26,
      "Y": 2
    },
    {
      "X": 25,
      "Y": 3
    },
    {
      "X": 23,
      "Y": 5
    },
    {
      "X": 22,
      "Y": 6
    },
    {
      "X": 41,
      "Y": 7
    },
    {
      "X": 41,
      "Y": 6
    },
    {
      "X": 40,
      "Y": 7
    },
    {
      "X": 40,
      "Y": 6
    },
    {
      "X": 40,
      "Y": 8
    },
    {
      "X": 39,
      "Y": 7
    },
    {
      "X": 40,
      "Y": 5
    },
    {
      "X": 39,
      "Y": 6
    },
    {
      "X": 39,
      "Y": 8
    },
    {
      "X": 38,
      "Y": 7
    },
    {
      "X": 39,
      "Y": 5
    },
    {
      "X": 38,
      "Y": 6
    },
    {
      "X": 38,
      "Y": 8
    },
    {
      "X": 37,
      "Y": 7
    },
    {
      "X": 38,
      "Y": 5
    },
    {
      "X": 37,
      "Y": 6
    },
    {
      "X": 38,
      "Y": 9
    },
    {
      "X": 37,
      "Y": 8
    },
    {
      "X": 36,
      "Y": 7
    },
    {
      "X": 38,
      "Y": 4
    },
    {
      "X": 37,
      "Y": 5
    },
    {
      "X": 36,
      "Y": 6
    },
    {
      "X": 38,
      "Y": 10
    },
    {
      "X": 37,
      "Y": 9
    },
    {
      "X": 36,
      "Y": 8
    },
    {
      "X": 35,
      "Y": 7
    },
    {
      "X": 38,
      "Y": 3
    },
    {
      "X": 37,
      "Y": 4
    },
    {
      "X": 36,
      "Y": 5
    },
    {
      "X": 35,
      "Y": 6
    },
    {
      "X": 37,
      "Y": 10
    },
    {
      "X": 36,
      "Y": 9
    },
    {
      "X": 35,
      "Y": 8
    },
    {
      "X": 34,
      "Y": 7
    },
    {
      "X": 37,
      "Y": 3
    },
    {
      "X": 36,
      "Y": 4
    },
    {
      "X": 35,
      "Y": 5
    },
    {
      "X": 34,
      "Y": 6
    },
    {
      "X": 37,
      "Y": 11
    },
    {
      "X": 36,
      "Y": 10
    },
    {
      "X": 35,
      "Y": 9
    },
    {
      "X": 34,
      "Y": 8
    },
    {
      "X": 33,
      "Y": 7
    },
    {
      "X": 37,
      "Y": 2
    },
    {
      "X": 36,
      "Y": 3
    },
    {
      "X": 35,
      "Y": 4
    },
    {
      "X": 34,
      "Y": 5
    },
    {
      "X": 33,
      "Y": 6
    },
    {
      "X": 36,
      "Y": 11
    },
    {
      "X": 35,
      "Y": 10
    },
    {
      "X": 33,
      "Y": 8
    },
    {
      "X": 32,
      "Y": 7
    },
    {
      "X": 36,
      "Y": 2
    },
    {
      "X": 35,
      "Y": 3
    },
    {
      "X": 33,
      "Y": 5
    },
    {
      "X": 32,
      "Y": 6
    },
    {
      "X": 27,
      "Y": 1
    },
    {
      "X": 27,
      "Y": 0
    },
    {
      "X": 26,
      "Y": 1
    },
    {
      "X": 27,
      "Y": -1
    },
    {
      "X": 28,
      "Y": 0
    },
    {
      "X": 26,
      "Y": 0
    },
    {
      "X": 27,
      "Y": -2
    },
    {
      "X": 28,
      "Y": -1
    },
    {
      "X": 26,
      "Y": -1
    },
    {
      "X": 25,
      "Y": 0
    },
    {
      "X": 27,
      "Y": -3
    },
    {
      "X": 28,
      "Y": -2
    },
    {
      "X": 26,
      "Y": -2
    },
    {
      "X": 29,
      "Y": -1
    },
    {
      "X": 25,
      "Y": -1
    },
    {
      "X": 27,
      "Y": -4
    },
    {
      "X": 28,
      "Y": -3
    },
    {
      "X": 25,
      "Y": -2
    },
    {
      "X": 30,
      "Y": -1
    },
    {
      "X": 24,
      "Y": -1
    },
    {
      "X": 27,
      "Y": -5
    },
    {
      "X": 29,
      "Y": -2
    },
    {
      "X": 24,
      "Y": -2
    },
    {
      "X": 30,
      "Y": -2
    },
    {
      "X": 23,
      "Y": -1
    },
    {
      "X": 27,
      "Y": -6
    },
    {
      "X": 28,
      "Y": -5
    },
    {
      "X": 26,
      "Y": -5
    },
    {
      "X": 23,
      "Y": -2
    },
    {
      "X": 30,
      "Y": -3
    },
    {
      "X": 27,
      "Y": -7
    },
    {
      "X": 28,
      "Y": -6
    },
    {
      "X": 26,
      "Y": -6
    },
    {
      "X": 25,
      "Y": -5
    },
    {
      "X": 23,
      "Y": -3
    },
    {
      "X": 30,
      "Y": -4
    },
    {
      "X": 27,
      "Y": -8
    },
    {
      "X": 28,
      "Y": -7
    },
    {
      "X": 26,
      "Y": -7
    },
    {
      "X": 29,
      "Y": -6
    },
    {
      "X": 25,
      "Y": -6
    },
    {
      "X": 24,
      "Y": -5
    },
    {
      "X": 23,
      "Y": -4
    },
    {
      "X": 30,
      "Y": -5
    },
    {
      "X": 26,
      "Y": -8
    },
    {
      "X": 25,
      "Y": -7
    },
    {
      "X": 30,
      "Y": -6
    },
    {
      "X": 24,
      "Y": -6
    },
    {
      "X": 23,
      "Y": -5
    },
    {
      "X": 23,
      "Y": -6
    },
    {
      "X": 29,
      "Y": -3
    },
    {
      "X": 47,
      "Y": 3
    },
    {
      "X": 47,
      "Y": 2
    },
    {
      "X": 46,
      "Y": 3
    },
    {
      "X": 47,
      "Y": 1
    },
    {
      "X": 46,
      "Y": 2
    },
    {
      "X": 46,
      "Y": 1
    },
    {
      "X": 47,
      "Y": 9
    },
    {
      "X": 47,
      "Y": 8
    },
    {
      "X": 46,
      "Y": 9
    },
    {
      "X": 47,
      "Y": 7
    },
    {
      "X": 48,
      "Y": 8
    },
    {
      "X": 46,
      "Y": 8
    },
    {
      "X": 47,
      "Y": 6
    },
    {
      "X": 48,
      "Y": 7
    },
    {
      "X": 46,
      "Y": 7
    },
    {
      "X": 49,
      "Y": 8
    },
    {
      "X": 45,
      "Y": 8
    },
    {
      "X": 47,
      "Y": 5
    },
    {
      "X": 48,
      "Y": 6
    },
    {
      "X": 46,
      "Y": 6
    },
    {
      "X": 49,
      "Y": 7
    },
    {
      "X": 45,
      "Y": 7
    },
    {
      "X": 50,
      "Y": 8
    },
    {
      "X": 44,
      "Y": 8
    },
    {
      "X": 47,
      "Y": 4
    },
    {
      "X": 48,
      "Y": 5
    },
    {
      "X": 46,
      "Y": 5
    },
    {
      "X": 49,
      "Y": 6
    },
    {
      "X": 45,
      "Y": 6
    },
    {
      "X": 50,
      "Y": 7
    },
    {
      "X": 44,
      "Y": 7
    },
    {
      "X": 43,
      "Y": 8
    },
    {
      "X": 46,
      "Y": 4
    },
    {
      "X": 49,
      "Y": 5
    },
    {
      "X": 45,
      "Y": 5
    },
    {
      "X": 50,
      "Y": 6
    },
    {
      "X": 44,
      "Y": 6
    },
    {
      "X": 51,
      "Y": 7
    },
    {
      "X": 43,
      "Y": 7
    },
    {
      "X": 50,
      "Y": 5
    },
    {
      "X": 44,
      "Y": 5
    },
    {
      "X": 51,
      "Y": 6
    },
    {
      "X": 43,
      "Y": 6
    },
    {
      "X": 42,
      "Y": 7
    },
    {
      "X": 43,
      "Y": 5
    },
    {
      "X": 42,
      "Y": 6
    }
  ],
  "WallOpenSides": [
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
      "Y": 9,
      "Side": "West"
    },
    {
      "X": 13,
      "Y": 10,
      "Side": "West"
    },
    {
      "X": 14,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 14,
      "Y": 11,
      "Side": "South"
    },
    {
      "X": 15,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 15,
      "Y": 11,
      "Side": "South"
    },
    {
      "X": 16,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 16,
      "Y": 5,
      "Side": "East"
    },
    {
      "X": 16,
      "Y": 6,
      "Side": "East"
    },
    {
      "X": 16,
      "Y": 7,
      "Side": "East"
    },
    {
      "X": 16,
      "Y": 8,
      "Side": "East"
    },
    {
      "X": 16,
      "Y": 11,
      "Side": "South"
    },
    {
      "X": 17,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 17,
      "Y": 5,
      "Side": "West"
    },
    {
      "X": 17,
      "Y": 6,
      "Side": "West"
    },
    {
      "X": 17,
      "Y": 7,
      "Side": "West"
    },
    {
      "X": 17,
      "Y": 8,
      "Side": "West"
    },
    {
      "X": 17,
      "Y": 11,
      "Side": "South"
    },
    {
      "X": 18,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 18,
      "Y": 11,
      "Side": "South"
    },
    {
      "X": 19,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 19,
      "Y": 11,
      "Side": "South"
    },
    {
      "X": 20,
      "Y": 3,
      "Side": "East"
    },
    {
      "X": 20,
      "Y": 4,
      "Side": "East"
    },
    {
      "X": 20,
      "Y": 9,
      "Side": "East"
    },
    {
      "X": 20,
      "Y": 10,
      "Side": "East"
    },
    {
      "X": 12,
      "Y": 5,
      "Side": "West"
    },
    {
      "X": 12,
      "Y": 8,
      "Side": "West"
    },
    {
      "X": 21,
      "Y": 5,
      "Side": "East"
    },
    {
      "X": 21,
      "Y": 8,
      "Side": "East"
    },
    {
      "X": 24,
      "Y": 3,
      "Side": "West"
    },
    {
      "X": 24,
      "Y": 10,
      "Side": "West"
    },
    {
      "X": 25,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 25,
      "Y": 11,
      "Side": "South"
    },
    {
      "X": 28,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 28,
      "Y": 11,
      "Side": "South"
    },
    {
      "X": 29,
      "Y": 3,
      "Side": "East"
    },
    {
      "X": 29,
      "Y": 10,
      "Side": "East"
    },
    {
      "X": 22,
      "Y": 5,
      "Side": "West"
    },
    {
      "X": 22,
      "Y": 8,
      "Side": "West"
    },
    {
      "X": 23,
      "Y": 4,
      "Side": "North"
    },
    {
      "X": 23,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 30,
      "Y": 4,
      "Side": "North"
    },
    {
      "X": 30,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 31,
      "Y": 5,
      "Side": "East"
    },
    {
      "X": 31,
      "Y": 8,
      "Side": "East"
    },
    {
      "X": 22,
      "Y": 14,
      "Side": "West"
    },
    {
      "X": 22,
      "Y": 15,
      "Side": "West"
    },
    {
      "X": 22,
      "Y": 16,
      "Side": "West"
    },
    {
      "X": 22,
      "Y": 17,
      "Side": "West"
    },
    {
      "X": 22,
      "Y": 18,
      "Side": "West"
    },
    {
      "X": 22,
      "Y": 19,
      "Side": "West"
    },
    {
      "X": 23,
      "Y": 13,
      "Side": "North"
    },
    {
      "X": 23,
      "Y": 20,
      "Side": "South"
    },
    {
      "X": 26,
      "Y": 15,
      "Side": "East"
    },
    {
      "X": 26,
      "Y": 18,
      "Side": "East"
    },
    {
      "X": 30,
      "Y": 13,
      "Side": "North"
    },
    {
      "X": 30,
      "Y": 20,
      "Side": "South"
    },
    {
      "X": 31,
      "Y": 14,
      "Side": "East"
    },
    {
      "X": 31,
      "Y": 19,
      "Side": "East"
    },
    {
      "X": 25,
      "Y": 12,
      "Side": "North"
    },
    {
      "X": 28,
      "Y": 12,
      "Side": "North"
    },
    {
      "X": 25,
      "Y": 21,
      "Side": "South"
    },
    {
      "X": 28,
      "Y": 21,
      "Side": "South"
    },
    {
      "X": 22,
      "Y": -6,
      "Side": "West"
    },
    {
      "X": 22,
      "Y": -5,
      "Side": "West"
    },
    {
      "X": 22,
      "Y": -4,
      "Side": "West"
    },
    {
      "X": 22,
      "Y": -3,
      "Side": "West"
    },
    {
      "X": 22,
      "Y": -2,
      "Side": "West"
    },
    {
      "X": 22,
      "Y": -1,
      "Side": "West"
    },
    {
      "X": 28,
      "Y": -8,
      "Side": "North"
    },
    {
      "X": 25,
      "Y": -8,
      "Side": "North"
    },
    {
      "X": 28,
      "Y": 1,
      "Side": "South"
    },
    {
      "X": 25,
      "Y": 1,
      "Side": "South"
    },
    {
      "X": 31,
      "Y": -1,
      "Side": "East"
    },
    {
      "X": 31,
      "Y": -2,
      "Side": "East"
    },
    {
      "X": 31,
      "Y": -3,
      "Side": "East"
    },
    {
      "X": 31,
      "Y": -4,
      "Side": "East"
    },
    {
      "X": 31,
      "Y": -5,
      "Side": "East"
    },
    {
      "X": 31,
      "Y": -6,
      "Side": "East"
    },
    {
      "X": 30,
      "Y": 0,
      "Side": "South"
    },
    {
      "X": 30,
      "Y": -7,
      "Side": "North"
    },
    {
      "X": 29,
      "Y": 0,
      "Side": "South"
    },
    {
      "X": 29,
      "Y": -7,
      "Side": "North"
    },
    {
      "X": 26,
      "Y": -3,
      "Side": "North"
    },
    {
      "X": 26,
      "Y": -4,
      "Side": "South"
    },
    {
      "X": 25,
      "Y": -3,
      "Side": "North"
    },
    {
      "X": 25,
      "Y": -4,
      "Side": "South"
    },
    {
      "X": 24,
      "Y": 0,
      "Side": "South"
    },
    {
      "X": 24,
      "Y": -3,
      "Side": "North"
    },
    {
      "X": 24,
      "Y": -4,
      "Side": "South"
    },
    {
      "X": 24,
      "Y": -7,
      "Side": "North"
    },
    {
      "X": 23,
      "Y": 0,
      "Side": "South"
    },
    {
      "X": 23,
      "Y": -7,
      "Side": "North"
    },
    {
      "X": 33,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 33,
      "Y": 4,
      "Side": "North"
    },
    {
      "X": 32,
      "Y": 8,
      "Side": "West"
    },
    {
      "X": 32,
      "Y": 5,
      "Side": "West"
    },
    {
      "X": 41,
      "Y": 8,
      "Side": "East"
    },
    {
      "X": 41,
      "Y": 5,
      "Side": "East"
    },
    {
      "X": 40,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 40,
      "Y": 4,
      "Side": "North"
    },
    {
      "X": 39,
      "Y": 10,
      "Side": "East"
    },
    {
      "X": 39,
      "Y": 3,
      "Side": "East"
    },
    {
      "X": 38,
      "Y": 11,
      "Side": "South"
    },
    {
      "X": 38,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 35,
      "Y": 11,
      "Side": "South"
    },
    {
      "X": 35,
      "Y": 2,
      "Side": "North"
    },
    {
      "X": 34,
      "Y": 10,
      "Side": "West"
    },
    {
      "X": 34,
      "Y": 3,
      "Side": "West"
    },
    {
      "X": 51,
      "Y": 8,
      "Side": "East"
    },
    {
      "X": 51,
      "Y": 5,
      "Side": "East"
    },
    {
      "X": 50,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 50,
      "Y": 4,
      "Side": "North"
    },
    {
      "X": 49,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 49,
      "Y": 4,
      "Side": "North"
    },
    {
      "X": 44,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 44,
      "Y": 4,
      "Side": "North"
    },
    {
      "X": 43,
      "Y": 9,
      "Side": "South"
    },
    {
      "X": 43,
      "Y": 4,
      "Side": "North"
    },
    {
      "X": 42,
      "Y": 8,
      "Side": "West"
    },
    {
      "X": 42,
      "Y": 5,
      "Side": "West"
    },
    {
      "X": 48,
      "Y": 11,
      "Side": "East"
    },
    {
      "X": 48,
      "Y": 10,
      "Side": "East"
    },
    {
      "X": 48,
      "Y": 3,
      "Side": "East"
    },
    {
      "X": 48,
      "Y": 2,
      "Side": "East"
    },
    {
      "X": 45,
      "Y": 11,
      "Side": "West"
    },
    {
      "X": 45,
      "Y": 10,
      "Side": "West"
    },
    {
      "X": 45,
      "Y": 3,
      "Side": "West"
    },
    {
      "X": 45,
      "Y": 2,
      "Side": "West"
    }
  ],
  "DoorEdges": [
    {
      "X": 21,
      "Y": 6,
      "Side": "East",
      "Id": "compartment-0-door-g28"
    },
    {
      "X": 21,
      "Y": 7,
      "Side": "East",
      "Id": "compartment-0-door-g28"
    },
    {
      "X": 11,
      "Y": 6,
      "Side": "East",
      "Id": "compartment-0-door-g29"
    },
    {
      "X": 11,
      "Y": 7,
      "Side": "East",
      "Id": "compartment-0-door-g29"
    },
    {
      "X": 36,
      "Y": 1,
      "Side": "South",
      "Id": "compartment-7-door-g24"
    },
    {
      "X": 37,
      "Y": 1,
      "Side": "South",
      "Id": "compartment-7-door-g24"
    },
    {
      "X": 26,
      "Y": 11,
      "Side": "South",
      "Id": "compartment-1-door-g24"
    },
    {
      "X": 27,
      "Y": 11,
      "Side": "South",
      "Id": "compartment-1-door-g24"
    },
    {
      "X": 26,
      "Y": -9,
      "Side": "South",
      "Id": "compartment-9-door-g28"
    },
    {
      "X": 27,
      "Y": -9,
      "Side": "South",
      "Id": "compartment-9-door-g28"
    },
    {
      "X": 27,
      "Y": 21,
      "Side": "South",
      "Id": "compartment-2-door-g26"
    },
    {
      "X": 26,
      "Y": 21,
      "Side": "South",
      "Id": "compartment-2-door-g26"
    },
    {
      "X": 37,
      "Y": 11,
      "Side": "South",
      "Id": "compartment-7-door-g25"
    },
    {
      "X": 36,
      "Y": 11,
      "Side": "South",
      "Id": "compartment-7-door-g25"
    },
    {
      "X": 31,
      "Y": 7,
      "Side": "East",
      "Id": "compartment-7-door-g23"
    },
    {
      "X": 31,
      "Y": 6,
      "Side": "East",
      "Id": "compartment-7-door-g23"
    },
    {
      "X": 41,
      "Y": 7,
      "Side": "East",
      "Id": "compartment-7-door-g22"
    },
    {
      "X": 41,
      "Y": 6,
      "Side": "East",
      "Id": "compartment-7-door-g22"
    },
    {
      "X": 47,
      "Y": 0,
      "Side": "South",
      "Id": "compartment-10-door-g34"
    },
    {
      "X": 46,
      "Y": 0,
      "Side": "South",
      "Id": "compartment-10-door-g34"
    },
    {
      "X": 26,
      "Y": 1,
      "Side": "South",
      "Id": "compartment-9-door-g29"
    },
    {
      "X": 51,
      "Y": 7,
      "Side": "East",
      "Id": "compartment-10-door-g28"
    },
    {
      "X": 27,
      "Y": 1,
      "Side": "South",
      "Id": "compartment-9-door-g29"
    },
    {
      "X": 51,
      "Y": 6,
      "Side": "East",
      "Id": "compartment-10-door-g28"
    },
    {
      "X": 47,
      "Y": 3,
      "Side": "South",
      "Id": "compartment-10-door-g33"
    },
    {
      "X": 46,
      "Y": 3,
      "Side": "South",
      "Id": "compartment-10-door-g33"
    },
    {
      "X": 47,
      "Y": 12,
      "Side": "South",
      "Id": "compartment-10-door-g32"
    },
    {
      "X": 46,
      "Y": 12,
      "Side": "South",
      "Id": "compartment-10-door-g32"
    },
    {
      "X": 47,
      "Y": 9,
      "Side": "South",
      "Id": "compartment-10-door-g31"
    },
    {
      "X": 46,
      "Y": 9,
      "Side": "South",
      "Id": "compartment-10-door-g31"
    }
  ],
  "WreckPatches": [],
  "JunctionWalls": []
}
""";
}
