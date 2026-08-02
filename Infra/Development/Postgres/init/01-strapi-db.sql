-- Runs once on first container start (empty data volume only).
-- Strapi gets its own database and role, separate from the application
-- database "myss".
CREATE ROLE strapi WITH LOGIN PASSWORD 'strapi-local-dev';
CREATE DATABASE strapi OWNER strapi;
