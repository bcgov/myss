# Local development image (compose.yaml mounts the source over /opt/app).
# A production build stage is added when this service is deployed for real.
FROM node:22-alpine

WORKDIR /opt/app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

ENV NODE_ENV=development
EXPOSE 1337

CMD ["npm", "run", "develop"]
