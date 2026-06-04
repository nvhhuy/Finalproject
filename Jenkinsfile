// BaseProject CI/CD — Option 1 (Harbor + SSH deploy + SonarQube).
// Setup guide: tools/jenkins/README.md
//
// Jenkins credentials (Manage Jenkins → Credentials):
//   harbor-credentials     — Username with password (Harbor robot account)
//   sonar-token            — Secret text (SonarQube user token)
//   deploy-ssh-key         — SSH Username with private key (VPS deploy user)

pipeline {
  agent any

  options {
    timestamps()
    disableConcurrentBuilds()
    buildDiscarder(logRotator(numToKeepStr: '30'))
  }

  parameters {
    choice(
      name: 'TARGET_ENV',
      choices: ['staging', 'production'],
      description: 'Deploy target environment (maps to deploy/env/<env>/ on VPS)'
    )
    choice(
      name: 'PIPELINE_ACTION',
      choices: ['deploy', 'rollback', 'smoke-only'],
      description: 'deploy = test→sonar→build→push→deploy; rollback = redeploy existing IMAGE_TAG; smoke-only = HTTP checks only'
    )
    string(
      name: 'IMAGE_TAG',
      defaultValue: '',
      description: 'Harbor image tag. Leave empty to use BUILD_NUMBER. Required for rollback.'
    )
    booleanParam(name: 'SKIP_TESTS', defaultValue: false, description: 'Skip CI test suite (not recommended for production deploy)')
    booleanParam(name: 'SKIP_SONAR', defaultValue: false, description: 'Skip SonarQube analysis')
    booleanParam(name: 'SKIP_DEPLOY', defaultValue: false, description: 'Build & push only — do not SSH deploy (still runs smoke if hosts reachable)')
  }

  environment {
    // ── Harbor (edit for your registry) ─────────────────────────────────────
    HARBOR_REGISTRY = 'harbor.example.com/baseproject'
    HARBOR_CREDENTIALS_ID = 'harbor-credentials'

    // ── SonarQube ───────────────────────────────────────────────────────────
    SONAR_HOST_URL = 'https://sonar.example.com'
    SONAR_PROJECT_KEY_BACKEND = 'baseproject-backend'
    SONAR_PROJECT_KEY_FRONTEND = 'baseproject-frontend'
    SONAR_PROJECT_KEY_ADMIN = 'baseproject-admin'
    SONAR_TOKEN_CREDENTIALS_ID = 'sonar-token'
    SONAR_SERVER_NAME = 'SonarQube'
    SONAR_QUALITY_GATE = 'true'

    // ── Deploy targets (SSH) ──────────────────────────────────────────────────
    DEPLOY_SSH_CREDENTIALS_ID = 'deploy-ssh-key'
    DEPLOY_SSH_USER = 'deploy'
    DEPLOY_REPO_PATH = '/opt/baseproject'
    DEPLOY_HOST_STAGING = 'staging-vps.example.com'
    DEPLOY_HOST_PRODUCTION = 'prod-vps.example.com'

    // ── Smoke test public hostnames (HTTPS via Traefik) ─────────────────────
    SMOKE_APP_HOST_STAGING = 'app.staging.example.com'
    SMOKE_ADMIN_HOST_STAGING = 'admin.staging.example.com'
    SMOKE_APP_HOST_PRODUCTION = 'app.example.com'
    SMOKE_ADMIN_HOST_PRODUCTION = 'admin.example.com'

    // ── Email notification ────────────────────────────────────────────────────
    NOTIFY_EMAIL = 'ops@example.com'

    // ── Derived at runtime ────────────────────────────────────────────────────
    IMAGE_REGISTRY = "${HARBOR_REGISTRY}"
    GIT_SHORT_SHA = "${env.GIT_COMMIT?.take(7) ?: 'unknown'}"
  }

  stages {
    stage('Validate') {
      steps {
        script {
          if (params.PIPELINE_ACTION == 'rollback' && !params.IMAGE_TAG?.trim()) {
            error('Rollback requires IMAGE_TAG — enter the Jenkins BUILD_NUMBER (or tag) to redeploy.')
          }
          env.EFFECTIVE_IMAGE_TAG = params.IMAGE_TAG?.trim() ?: "${env.BUILD_NUMBER}"
          env.DEPLOY_HOST = (params.TARGET_ENV == 'production')
            ? env.DEPLOY_HOST_PRODUCTION
            : env.DEPLOY_HOST_STAGING
          env.SMOKE_APP_HOST = (params.TARGET_ENV == 'production')
            ? env.SMOKE_APP_HOST_PRODUCTION
            : env.SMOKE_APP_HOST_STAGING
          env.SMOKE_ADMIN_HOST = (params.TARGET_ENV == 'production')
            ? env.SMOKE_ADMIN_HOST_PRODUCTION
            : env.SMOKE_ADMIN_HOST_STAGING

          echo """
            Pipeline summary
            ─────────────────
            Action     : ${params.PIPELINE_ACTION}
            Environment: ${params.TARGET_ENV}
            Image tag  : ${env.EFFECTIVE_IMAGE_TAG}
            Deploy host: ${env.DEPLOY_HOST}
            Smoke app  : https://${env.SMOKE_APP_HOST}/
            Smoke admin: https://${env.SMOKE_ADMIN_HOST}/
          """.stripIndent()
        }
      }
    }

    stage('Tests') {
      when {
        expression {
          params.PIPELINE_ACTION == 'deploy' && !params.SKIP_TESTS
        }
      }
      steps {
        sh '''
          chmod +x tools/ci/run-ci-local.sh tools/jenkins/scripts/*.sh
          ./tools/ci/run-ci-local.sh admin frontend backend media
        '''
      }
    }

    stage('SonarQube') {
      when {
        expression {
          params.PIPELINE_ACTION == 'deploy' && !params.SKIP_SONAR
        }
      }
      steps {
        withCredentials([string(credentialsId: env.SONAR_TOKEN_CREDENTIALS_ID, variable: 'SONAR_TOKEN')]) {
          script {
            parallel(
              'Backend': {
                withSonarQubeEnv("${SONAR_SERVER_NAME}") {
                  sh './tools/jenkins/scripts/sonar-backend.sh'
                }
              },
              'Frontend': {
                withSonarQubeEnv("${SONAR_SERVER_NAME}") {
                  sh './tools/jenkins/scripts/sonar-frontend.sh'
                }
              },
              'Admin': {
                withSonarQubeEnv("${SONAR_SERVER_NAME}") {
                  sh './tools/jenkins/scripts/sonar-admin.sh'
                }
              }
            )
          }
        }
      }
    }

    stage('SonarQube Quality Gate') {
      when {
        allOf {
          expression { params.PIPELINE_ACTION == 'deploy' && !params.SKIP_SONAR }
          expression { env.SONAR_QUALITY_GATE == 'true' }
        }
      }
      steps {
        withCredentials([string(credentialsId: env.SONAR_TOKEN_CREDENTIALS_ID, variable: 'SONAR_TOKEN')]) {
          timeout(time: 15, unit: 'MINUTES') {
            sh './tools/jenkins/scripts/sonar-quality-gate.sh'
          }
        }
      }
    }

    stage('Build & Push Harbor') {
      when {
        expression { params.PIPELINE_ACTION == 'deploy' }
      }
      steps {
        withCredentials([usernamePassword(
          credentialsId: env.HARBOR_CREDENTIALS_ID,
          usernameVariable: 'HARBOR_USER',
          passwordVariable: 'HARBOR_PASSWORD'
        )]) {
          sh '''
            export IMAGE_TAG="${EFFECTIVE_IMAGE_TAG}"
            ./tools/jenkins/scripts/harbor-login.sh
          '''
          script {
            parallel(
              'api': {
                sh '''
                  export IMAGE_TAG="${EFFECTIVE_IMAGE_TAG}"
                  ./tools/jenkins/scripts/build-push-one.sh api
                '''
              },
              'worker': {
                sh '''
                  export IMAGE_TAG="${EFFECTIVE_IMAGE_TAG}"
                  ./tools/jenkins/scripts/build-push-one.sh worker
                '''
              },
              'migrate': {
                sh '''
                  export IMAGE_TAG="${EFFECTIVE_IMAGE_TAG}"
                  ./tools/jenkins/scripts/build-push-one.sh migrate
                '''
              },
              'frontend': {
                sh '''
                  export IMAGE_TAG="${EFFECTIVE_IMAGE_TAG}"
                  ./tools/jenkins/scripts/build-push-one.sh frontend
                '''
              },
              'admin': {
                sh '''
                  export IMAGE_TAG="${EFFECTIVE_IMAGE_TAG}"
                  ./tools/jenkins/scripts/build-push-one.sh admin
                '''
              }
            )
          }
        }
      }
    }

    stage('Deploy VPS') {
      when {
        allOf {
          expression { params.PIPELINE_ACTION in ['deploy', 'rollback'] }
          expression { !params.SKIP_DEPLOY }
        }
      }
      steps {
        sshagent(credentials: [env.DEPLOY_SSH_CREDENTIALS_ID]) {
          withCredentials([usernamePassword(
            credentialsId: env.HARBOR_CREDENTIALS_ID,
            usernameVariable: 'HARBOR_USER',
            passwordVariable: 'HARBOR_PASSWORD'
          )]) {
            script {
              def skipMigrate = params.PIPELINE_ACTION == 'rollback' ? '1' : '0'
              sh """
                export IMAGE_TAG="\${EFFECTIVE_IMAGE_TAG}"
                export SKIP_MIGRATE=${skipMigrate}
                export RUN_VPS_SMOKE=1
                ./tools/jenkins/scripts/deploy-remote.sh \\
                  "\${TARGET_ENV}" \\
                  "\${DEPLOY_HOST}" \\
                  "\${DEPLOY_REPO_PATH}"
              """
            }
          }
        }
      }
    }

    stage('Smoke Test') {
      when {
        expression {
          params.PIPELINE_ACTION == 'smoke-only' || params.SKIP_DEPLOY
        }
      }
      steps {
        sh '''
          ./tools/jenkins/scripts/smoke-http.sh \
            "${SMOKE_APP_HOST}" \
            "${SMOKE_ADMIN_HOST}"
        '''
      }
    }
  }

  post {
    always {
      script {
        def action = params.PIPELINE_ACTION
        def tag = env.EFFECTIVE_IMAGE_TAG ?: params.IMAGE_TAG ?: env.BUILD_NUMBER
        def subject = "[BaseProject][${params.TARGET_ENV}] ${currentBuild.currentResult} #${env.BUILD_NUMBER} (${action})"
        def body = """
          BaseProject Jenkins pipeline finished.

          Result       : ${currentBuild.currentResult}
          Build        : #${env.BUILD_NUMBER}
          Action       : ${action}
          Environment  : ${params.TARGET_ENV}
          Image tag    : ${tag}
          Git SHA      : ${env.GIT_COMMIT ?: 'n/a'}
          Deploy host  : ${env.DEPLOY_HOST ?: 'n/a'}
          Smoke app    : https://${env.SMOKE_APP_HOST ?: 'n/a'}/
          Smoke admin  : https://${env.SMOKE_ADMIN_HOST ?: 'n/a'}/
          Console log  : ${env.BUILD_URL}console

          Rollback hint: re-run pipeline with PIPELINE_ACTION=rollback and IMAGE_TAG=<previous BUILD_NUMBER>.
        """.stripIndent()

        emailext(
          subject: subject,
          body: body,
          to: "${env.NOTIFY_EMAIL}",
          mimeType: 'text/plain'
        )
      }
    }
    success {
      echo "Pipeline succeeded. Images tagged: ${env.EFFECTIVE_IMAGE_TAG}"
    }
    failure {
      echo 'Pipeline failed — check console log and smoke/sonar stages.'
    }
  }
}
