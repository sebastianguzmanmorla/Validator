# Cargar variables desde .env si existe
ifneq ("$(wildcard .env)","")
    include .env
    export $(shell sed 's/=.*//' .env)
endif

# Variables
PROJECT_PATH=src/SebastianGuzmanMorla.Validator/SebastianGuzmanMorla.Validator.csproj
PACK_OUTPUT=bin/Release
NUGET_SOURCE=https://api.nuget.org/v3/index.json

# Detección de versión
VERSION=$(shell dotnet msbuild $(PROJECT_PATH) -nologo -t:PrintVersion -v:q 2>&1 | tail -1 | awk '{print $$NF}')

.PHONY: clean pack push version check-env

version:
	@echo "Versión detectada: $(VERSION)"

check-env:
	@if [ -z "$(API_KEY)" ]; then \
		echo "Error: API_KEY no encontrada. Asegúrate de tener un archivo .env con API_KEY=xxx"; \
		exit 1; \
	fi

clean:
	@echo "Limpiando binarios..."
	@rm -rf $(PACK_OUTPUT)
	@dotnet clean $(PROJECT_PATH) -c Release

build:
	@echo "Compilando SebastianGuzmanMorla.Validator $(VERSION)..."
	@dotnet build $(PROJECT_PATH) -c Release

pack: clean build
	@echo "Empaquetando SebastianGuzmanMorla.Validator $(VERSION)..."
	@dotnet pack $(PROJECT_PATH) -c Release -o $(PACK_OUTPUT)

push: check-env pack
	@echo "Publicando en NuGet..."
	@dotnet nuget push $(PACK_OUTPUT)/SebastianGuzmanMorla.Validator.$(VERSION).nupkg \
		--api-key $(API_KEY) \
		--source $(NUGET_SOURCE)