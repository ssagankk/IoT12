# Opis aplikacji

Aplikacja składa się z trzech głównych kontenerów, które współpracują ze sobą, umożliwiając użytkownikowi zarządzanie notatkami (dodawanie, przeglądanie). Wszystkie komponenty aplikacji są uruchamiane i zarządzane za pomocą Docker oraz Kubernetes.

## Kontenery aplikacji

### Kontener z bazą danych (PostgreSQL)
- **Nazwa kontenera**: `postgres_db`
- **Obraz**: `postgres:15`
- **Zadanie**: Przechowywanie danych o notatkach (tytuł, treść).
- **Baza danych**: `notes_db`
- **Port**: `5432` (port wewnętrzny w kontenerze).
- **Usługa w Kubernetes**: `postgres-db`
- **Persistent Storage**: Zastosowano `PersistentVolumeClaim`, by dane były trwałe i dostępne po restarcie kontenera.

### Kontener backendowy (API - FastAPI)
- **Nazwa kontenera**: `fastapi-backend`
- **Obraz**: Tworzony lokalnie z pliku `Dockerfile` w folderze `./backend`.
- **Zadanie**: Aplikacja backendowa stworzona za pomocą FastAPI, która zapewnia API do zarządzania notatkami (operacje typu `GET` i `POST`).
- **Port**: `8000`
- **Zmienne środowiskowe**: `DB_HOST`, `DB_NAME`, `DB_USER`, `DB_PASSWORD`.
- **Usługa w Kubernetes**: `fastapi-backend`

### Kontener frontendowy (Flask)
- **Nazwa kontenera**: `flask-frontend`
- **Obraz**: Tworzony lokalnie z pliku `Dockerfile` w folderze `./frontend`.
- **Zadanie**: Aplikacja frontendowa oparta na Flask, umożliwiająca użytkownikowi przeglądanie notatek oraz dodawanie nowych poprzez prosty formularz.
- **Port**: `5000`
- **Zmienne środowiskowe**: `BACKEND_URL`
- **Usługa w Kubernetes**: `flask-frontend`

---

## Jak uruchomić aplikację w Dockerze

### Przygotowanie środowiska:
1. Zainstaluj Docker oraz Docker Compose.

### Uruchomienie aplikacji:
```bash
docker-compose up --build
```
Komenda ta zbuduje obrazy kontenerów na podstawie lokalnego kodu źródłowego i uruchomi wszystkie komponenty aplikacji (`backend`, `frontend`, `db`).

### Sprawdzanie poprawności działania:
```bash
docker ps
```
Otwórz przeglądarkę i wejdź na adres `http://localhost:5000`, aby zobaczyć działającą aplikację.

---

## Jak uruchomić aplikację w Kubernetes

### Uruchomienie aplikacji:
```bash
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/service.yaml
kubectl apply -f k8s/ingress.yaml
kubectl apply -f k8s/persistentvolumeclaim.yaml
```

### Sprawdzanie statusu aplikacji:
```bash
kubectl get pods
kubectl get svc
```

### Zewnętrzny dostęp do aplikacji:
Jeśli masz `Ingress`, sprawdź adres IP lub nazwę domeny przypisaną do `Ingress`:
```bash
kubectl get ingress
```
Następnie wprowadź adres w przeglądarkę, aby zobaczyć działającą aplikację (np. `http://flask-frontend.local:5000`).

---

## Dokumentacja plików

### `docker-compose.yaml`:
- Definiuje usługi dla trzech kontenerów: `backend` (FastAPI), `frontend` (Flask) i `baza danych` (PostgreSQL).
- Używa zmiennych środowiskowych, aby kontenery mogły się ze sobą komunikować, np. backend komunikuje się z bazą danych za pomocą zmiennej `DB_HOST`.

### `deployment.yaml`:
- Definiuje zasoby dla kontenerów w Kubernetes: `backend`, `frontend` i `baza danych`.
- Ustawia zmienne środowiskowe i połączenia między nimi.

### `service.yaml`:
- Określa usługi Kubernetes, umożliwiając kontenerom wzajemną komunikację oraz dostęp z zewnątrz (np. porty `5000` i `8000`).

### `ingress.yaml`:
- Umożliwia dostęp do aplikacji przez zewnętrzny adres URL, zarządzając regułami routingu.

### `persistentvolumeclaim.yaml`:
- Umożliwia przechowywanie danych bazy PostgreSQL na trwałym dysku w Kubernetes, zapewniając dostępność danych nawet po restarcie podów.

---

## Sprawdzanie poprawności działania

### Frontend:
- Otwórz przeglądarkę i wejdź na adres `http://localhost:<port>/`.
```bash
kubectl get svc
```

```pgsql
NAME              TYPE        CLUSTER-IP      EXTERNAL-IP   PORT(S)          AGE
fastapi-backend   ClusterIP   10.99.13.228    <none>        8000/TCP         27m
flask-frontend    NodePort    10.103.1.196    <none>        5000:31438[ten port]/TCP   27m

```
- Powinna pojawić się lista notatek oraz formularz do dodawania nowych.

### Backend (API):
- API backendowe dostępne jest pod `http://localhost:8000/docs`.
- Można testować je za pomocą narzędzi takich jak `Postman` lub `curl`.

### Baza danych:
- Można połączyć się z bazą danych PostgreSQL używając narzędzi takich jak `pgAdmin`, `DBeaver` lub terminal.
- Dane dostępowe: `host` i `port 10.99.222.111:5432`, użytkownik `user`, hasło `password`.

### Logi:
Aby monitorować działanie aplikacji, sprawdzaj logi kontenerów:
```bash
kubectl logs [nazwa_poda]
```

---

## Podsumowanie
Aplikacja składa się z trzech komponentów: **PostgreSQL**, **FastAPI** (backend) oraz **Flask** (frontend). Kontenery są zbudowane przy pomocy Docker i zarządzane w Kubernetes, umożliwiając łatwe uruchamianie, skalowanie i monitorowanie aplikacji. Dostęp do aplikacji frontendowej jest możliwy przez Ingress, a backend komunikuje się z bazą danych w celu zarządzania notatkami.
