db = db.getSiblingDB('backpack');

db.createCollection('backpack-processors');

db["backpack-processors"].insertMany([
  {
    _id: 'npm',
    config: {},
    direct_collect: false,
    description: 'Node.js Package Manager'
  },
  {
    _id: 'nuget',
    config: {},
    direct_collect: false,
    description: '.NET Package Manager'
  },
  {
    _id: 'git',
    config: {},
    direct_collect: true,
    description: 'Git Repository Mirroring'
  },
  {
    _id: 'container',
    config: {},
    direct_collect: false,
    description: 'OCI/Docker Container Mirroring'
  },
  {
    _id: 'pypi',
    config: {},
    direct_collect: false,
    description: 'Python Package Index'
  },
  {
    _id: 'maven',
    config: {
      "group": {
        "key": "group",
        "type": "string",
        "name": "Group ID",
        "placeholder": ""
      },
    },
    direct_collect: false,
    description: 'Java Maven Repositories'
  },
  {
    _id: 'helm',
    config: {},
    direct_collect: false,
    description: 'Kubernetes Helm Charts'
  }
]);
