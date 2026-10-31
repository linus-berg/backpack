db = db.getSiblingDB('backpack');

db.createCollection('backpack-processors');

db["backpack-processors"].insertMany([
  {
    _id: 'npm',
    config: {},
    direct_collect: false,
    description: ''
  },
  {
    _id: 'nuget',
    config: {},
    direct_collect: false,
    description: ''
  },
  {
    _id: 'git',
    config: {},
    direct_collect: true,
    description: ''
  },
  {
    _id: 'direct',
    config: {},
    direct_collect: true,
    description: ''
  },
  {
    _id: 'container',
    config: {},
    direct_collect: false,
    description: ''
  },
  {
    _id: 'jetbrains-plugins',
    config: {},
    direct_collect: false,
    description: ''
  },
  {
    _id: 'jetbrains-ide',
    config: {},
    direct_collect: false,
    description: ''
  },
  {
    _id: 'pypi',
    config: {},
    direct_collect: false,
    description: ''
  },
  {
    _id: 'rancher',
    config: {
      "file": {
        "key": "file",
        "type": "string",
        "name": "File name in github release",
        "placeholder": ""
      }
    },
    direct_collect: false,
    description: ''
  },
  {
    _id: 'helm',
    config: {},
    direct_collect: false,
    description: ''
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
    description: ''
  }
]);
