using System;
using System.Linq;
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
        private float _heatElapsed;
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
            if (_definition == null || _definition.HeatRadius <= 0f || _definition.HeatPerSecond == 0f) return;

            var playerCamera = PlayerCamera.main;
            var body = playerCamera ? playerCamera.body : null;
            if (!body)
            {
                _heatElapsed = 0f;
                return;
            }

            _heatElapsed += Time.deltaTime;
            if (_heatElapsed < 1f) return;

            ApplyHeatAura(body, _heatElapsed);
            _heatElapsed = 0f;
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

        internal void DestroyForWorldClear()
        {
            _isQuitting = true;
            Destroy(gameObject);
        }

        private void ApplyHeatAura(Body body, float elapsed)
        {
            var distance = Vector2.Distance(transform.position, body.transform.position);
            if (distance > _definition.HeatRadius) return;

            var targetTemperature = _definition.MaxHeatBodyTemperature > 0f
                ? _definition.MaxHeatBodyTemperature
                : float.MaxValue;

            if (body.temperature >= targetTemperature) return;

            body.temperature = Mathf.Min(targetTemperature,
                body.temperature + _definition.HeatPerSecond * elapsed);
        }

        private void ApplySpawnComponents()
        {
            if (_definition?.SpawnComponents == null
                || _definition.SpawnComponents.Count == 0) return;

            foreach (var componentType in from componentName in _definition.SpawnComponents
                     where !string.IsNullOrWhiteSpace(componentName)
                     select Type.GetType(componentName, false)
                     into componentType
                     where componentType != null && typeof(MonoBehaviour).IsAssignableFrom(componentType)
                     where GetComponent(componentType) == null
                     select componentType)
            {
                gameObject.AddComponent(componentType);
            }
        }
    }
}