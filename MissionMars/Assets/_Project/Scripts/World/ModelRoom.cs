using System;
using UnityEngine;

namespace MissionMars
{
    public class ModelRoom
    {
        public GameObject Room;
        public Vector3 Spawn;
        public float SpawnYaw;
        public Interactable ExitDoor;
        public Transform ExitPanel;

        public static GameObject Place(string model, Transform parent, Vector3 position, float yaw = 0)
        {
            var prefab = Resources.Load<GameObject>("MarsBase9/" + model);
            var instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            return instance;
        }

        public static Transform Part(GameObject module, string prefix)
        {
            foreach (var part in module.GetComponentsInChildren<Transform>(true))
                if (part.name.StartsWith(prefix, StringComparison.Ordinal)) return part;
            throw new InvalidOperationException($"Missing {prefix} in {module.name}");
        }

        public static void AddColliders(GameObject model)
        {
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
        }

        public static SlideDoor SlidingPanel(Transform panel)
        {
            var door = panel.gameObject.AddComponent<SlideDoor>();
            door.Closed = panel.localPosition;
            door.Direction = Vector3.forward;
            return door;
        }

        public static ModelRoom Build(string model, Vector3 origin, float length, float width)
        {
            var room = Place(model, null, origin);
            AddColliders(room);
            var door = Part(room, "Centered short-wall");
            SlidingPanel(door);
            ProcMesh.Prim(PrimitiveType.Cube, "Ceiling", room.transform, new Vector3(0, 2.65f, 0),
                new Vector3(length + 0.2f, 0.1f, width + 0.2f), R.Panel);
            var lining = R.Lit(new Color(0.57f, 0.61f, 0.64f), 0.25f);
            foreach (float side in new[] { -1f, 1f })
            {
                ProcMesh.Prim(PrimitiveType.Cube, "Upper side wall", room.transform,
                    new Vector3(0, 2.29f, side * width / 2), new Vector3(length, 0.72f, 0.12f), lining);
                ProcMesh.Prim(PrimitiveType.Cube, "Upper end wall", room.transform,
                    new Vector3(side * length / 2, 2.29f, 0), new Vector3(0.12f, 0.72f, width), lining);
                var light = new GameObject("Cabin light").AddComponent<Light>();
                light.transform.SetParent(room.transform, false);
                light.transform.localPosition = new Vector3(side * length * 0.25f, 2.4f, 0);
                light.range = length;
                light.intensity = 2f;
            }
            return new ModelRoom {
                Room = room, ExitPanel = door,
                ExitDoor = Interactable.Add(door.gameObject, "Go outside", "Mars base", null),
                Spawn = origin + new Vector3(-length / 2 + 1.1f, 0.08f, 0), SpawnYaw = 90
            };
        }

        public Interactable UsePart(string prefix, string verb, string label, Action use = null)
        {
            return Interactable.Add(Part(Room, prefix).gameObject, verb, label, use);
        }
    }
}
