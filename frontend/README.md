# Frontend

This project was generated with [Angular CLI](https://github.com/angular/angular-cli) version 17.3.17.

## Development server

Run `ng serve` for a dev server. Navigate to `http://localhost:4200/`. The application will automatically reload if you change any of the source files.

## Code scaffolding

Run `ng generate component component-name` to generate a new component. You can also use `ng generate directive|pipe|service|class|guard|interface|enum|module`.

## Build

Run `ng build` to build the project. The build artifacts will be stored in the `dist/` directory.

## Running unit tests

Run `ng test` to execute the unit tests via [Karma](https://karma-runner.github.io).

## Running end-to-end tests

Run `ng e2e` to execute the end-to-end tests via a platform of your choice. To use this command, you need to first add a package that implements end-to-end testing capabilities.

## Further help

To get more help on the Angular CLI use `ng help` or go check out the [Angular CLI Overview and Command Reference](https://angular.io/cli) page.



Y asegúrate que la definición de tarea en ECS esté configurada con:

Arquitectura: Linux/X86_64

Fargate (como ya lo tienes)



docker buildx build \
  --platform linux/amd64 \
  -t 196080479890.dkr.ecr.us-east-2.amazonaws.com/sanmarino/zootecnia/granjas/frontend:latest \
  --push .


Después de guardar este package.json, corre:

yarn build:ssr
yarn compile:server


## **********  Actualizar **********************

# 0) Variables
$REGION  = "us-east-2"
$BUCKET  = "sanmarino-frontend-021891592771-us-east-2"
$DIST_ID = "EBH3ELXXF2N7T"

# 1) Build producción (Angular)
ng build --configuration production

# 2) Subir a S3 (tu distribución usa OriginPath=/browser)
aws s3 sync ".\dist" "s3://$BUCKET" --delete

# 3) Invalidar CloudFront (para que tome el nuevo build)
aws cloudfront create-invalidation --distribution-id $DIST_ID --paths "/*"

# 4) (Opcional) Ver estado de la invalidación y la distro
aws cloudfront get-distribution --id $DIST_ID --query "Distribution.Status" --output text


