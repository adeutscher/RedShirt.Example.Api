# Local Testing Instructions

1. Bring up `ministack` container:

    ```bash
    docker compose up -d ministack
    ```

2. Run `make-local-resources.sh` script to create ministack resources:

    ```bash
    ./make-local-resources.sh
    ```

3. Bring up the `api` container

    ```bash
    docker compose up api
    ```

4. Visit the Swagger page at http://localhost:9000/swagger/