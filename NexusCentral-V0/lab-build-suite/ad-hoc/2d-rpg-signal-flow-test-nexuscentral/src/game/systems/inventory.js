export function createInventory() {
  return {
    items: [],
    add(item) {
      if (!this.items.includes(item)) this.items.push(item);
    }
  };
}
