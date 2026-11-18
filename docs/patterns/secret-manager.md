# Secret Manager

Many components of this template rely on an implementation of the `ISecretManagerService` interface to function. Use of
a secret manager is highly encouraged for values that could have sensitive information such as credentials to external
services.

Currently, the template provides 3 possible implementations:

* [AWS SSM Parameter Store](https://docs.aws.amazon.com/systems-manager/latest/userguide/systems-manager-parameter-store.html)
* [Azure Key Vault](https://azure.microsoft.com/en-us/products/key-vault)
* [Docker Secrets](https://docs.docker.com/reference/compose-file/secrets/)
  ([Secondary Link](https://docs.docker.com/engine/swarm/secrets/))

## Docker Secrets

While the other secret managers are more straightforward with their plans, Docker Secrets has some caveats and
assumptions that should be documented.

In general, I would encourage the use of a secret manager other than Docker Secrets. Compared to other options it lacks
flexibility. However, it may be exactly what is needed for a small-scale environment.

Other notes:

* Unlike other secret managers, a container instance's secrets cannot be rotated without restarting the
* Secrets are assumed to be files in `/run/secrets`
    * This directory can be overridden by setting a new path in `COMMON__SECRETS__DOCKER__DIRECTORY`
* Secret keys are assumed to be roughly equal to the underlying file specified in the Docker stack configuration. If the
  file does not meet these guidelines and because the compose file specifies another target path within the container,
  then the secret manager has nothing with which to resolve a key to a file containing a value. After checking the exact
  name of the key under the secret directory, the secret manager will attempt the path with a few file extensions
  (though realistically, only the flat key match will probably be useful).
    * For example, with no overriding directory the key `foo-password` will be searched for under the following absolute
      paths:
        * `/run/secrets/foo-password`
        * `/run/secrets/foo-password.txt`
        * `/run/secrets/foo-password.json`
    * In general, it's advised not to meddle with secret targets within the container at all if you plan to use them
      with this template's Docker secret manager.
* The implementation has not been tested under Docker Swarm, and as such hasn't been tested with external secrets.