using System;
using CUCoreLib.Data;
using CUCoreLib.Registries;
using UnityEngine;

namespace CUCoreLib.Helpers
{
    internal sealed class CustomBuildingRuntime : MonoBehaviour
    {
        public string DefinitionId;

        private BuildingEntity _building;
        private CustomBuildingEntityDefinition _definition;
        private bool _isQuitting;
        private bool _registered;
        private bool _spawnedDrops;

        private void Awake()
        {
            _building = GetComponent<BuildingEntity>();
            BuildingEntityRegistry.TryGetDefinition(DefinitionId, out _definition);
            ApplySpawnComponents();
            BuildingEntityRegistry.ApplyInstanceConfiguration(gameObject, DefinitionId);
        }

        private void Update()
        {
            ApplyHeatAura();
        }

        private void OnEnable()
        {
            if (_registered || string.IsNullOrWhiteSpace(DefinitionId)) return;

            BuildingEntityRegistry.RegisterRuntime(this);
            _registered = true;
        }

        private void OnDisable()
        {
            if (!_registered) return;

            BuildingEntityRegistry.UnregisterRuntime(this);
            _registered = false;
        }

        private void OnDestroy()
        {
            if (_registered)
            {
                BuildingEntityRegistry.UnregisterRuntime(this);
                _registered = false;
            }

            if (_isQuitting || _spawnedDrops) return;
            if (_building == null || _building.health >= 0.5f) return;

            _spawnedDrops = true;
            BuildingEntityRegistry.SpawnDrops(_building, DefinitionId);
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        private void ApplyHeatAura()
        {
            if (_definition == null || _definition.HeatRadius <= 0f || _definition.HeatPerSecond == 0f) return;

            var playerCamera = PlayerCamera.main;
            var body = playerCamera != null ? playerCamera.body : null;
            if (body == null) return;

            var distance = Vector2.Distance(transform.position, body.transform.position);
            if (distance > _definition.HeatRadius) return;

            var targetTemperature = _definition.MaxHeatBodyTemperature > 0f
                ? _definition.MaxHeatBodyTemperature
                : float.MaxValue;

            if (body.temperature >= targetTemperature) return;

            body.temperature = Mathf.Min(targetTemperature,
                body.temperature + _definition.HeatPerSecond * Time.deltaTime);
        }

        private void ApplySpawnComponents()
        {
            if (_definition == null || _definition.SpawnComponents == null ||
                _definition.SpawnComponents.Count == 0) return;

            foreach (var componentName in _definition.SpawnComponents)
            {
                if (string.IsNullOrWhiteSpace(componentName)) continue;

                var componentType = Type.GetType(componentName, false);
                if (componentType == null || !typeof(MonoBehaviour).IsAssignableFrom(componentType)) continue;

                if (GetComponent(componentType) == null) gameObject.AddComponent(componentType);
            }
        }
    }
}