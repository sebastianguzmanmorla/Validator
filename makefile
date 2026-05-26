# Cargar variables desde .env si existe
ifneq ("$(wildcard .env)","")
    include .env
endif

# Variables
PROJECT_PATH=src/SebastianGuzmanMorla.Validator/SebastianGuzmanMorla.Validator.csproj
PACK_OUTPUT=artifacts
NUGET_SOURCE=https://api.nuget.org/v3/index.json

# Comandos dependientes del sistema operativo
ifeq ($(OS),Windows_NT)
    RM_DIR = if exist "$(subst /,\,$(PACK_OUTPUT))" rmdir /s /q "$(subst /,\,$(PACK_OUTPUT))"
else
    RM_DIR = rm -rf $(PACK_OUTPUT)
endif

.PHONY: clean pack push check-env

check-env:
ifndef API_KEY
	$(error API_KEY no encontrada. Asegurate de tener un archivo .env con API_KEY=xxx)
endif

clean:
	@echo "Limpiando binarios..."
	@-$(RM_DIR)
	@dotnet clean $(PROJECT_PATH) -c Release

build:
	@echo "Compilando SebastianGuzmanMorla.Validator..."
	@dotnet build $(PROJECT_PATH) -c Release

pack: clean build
	@echo "Empaquetando SebastianGuzmanMorla.Validator..."
	@dotnet pack $(PROJECT_PATH) -c Release -o $(PACK_OUTPUT)

push: check-env pack
	@echo "Publicando en NuGet..."
	@dotnet nuget push $(PACK_OUTPUT)/*.nupkg \
		--api-key $(API_KEY) \
		--source $(NUGET_SOURCE) \
		--skip-duplicate