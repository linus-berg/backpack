// Seeds the processor definitions for the example stack. MongoDB runs this
// only when its data volume is empty. Every processor service you deploy needs
// a definition whose _id matches the endpoint it registers (processor-<id>);
// administrators can also add and edit them on the GUI's Processors page.
// Field reference: ../docs/Configuration.md#processor-definitions
db = db.getSiblingDB('backpack');

db.createCollection('backpack-processors');

db['backpack-processors'].insertMany([
  {
    _id: 'npm',
    description: 'Node.js packages from registry.npmjs.org.',
    config: {},
    direct_collect: false,
    requires_approval: false,
    multi_add: true,
    is_external: false,
    preview_enabled: true
  }
  // A direct-collect definition sends the artifact ID (a URL) straight to the
  // collector for its scheme. This one needs a Collector.Git service:
  // {
  //   _id: 'git',
  //   description: 'Git repositories, e.g. git://github.com/owner/repo',
  //   config: {},
  //   direct_collect: true,
  //   requires_approval: false,
  //   multi_add: true,
  //   is_external: false,
  //   preview_enabled: false
  // }
]);
